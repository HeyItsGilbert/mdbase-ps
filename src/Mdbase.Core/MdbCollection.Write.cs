using System.Collections.Specialized;
using System.Text;
using Mdbase.Core.Cel;
using Mdbase.Core.Compose;
using Mdbase.Core.Json;
using Mdbase.Core.Loading;
using Mdbase.Core.Write;
using Mdbase.Core.Yaml;

namespace Mdbase.Core;

/// <summary>
/// The Core Write half of <see cref="MdbCollection"/> (#41): <see cref="Create"/>/<see cref="Update"/>/
/// <see cref="Delete"/>/<see cref="Rename"/>/<see cref="ExecuteBatch"/>. Every method follows
/// Ch.09/Ch.12's validation order exactly and reuses <see cref="Refresh"/> for index maintenance
/// — no second index-maintenance code path.
/// </summary>
public sealed partial class MdbCollection
{
    /// <summary>
    /// Builds a new record from <paramref name="frontmatter"/> and an optional <paramref name="body"/>
    /// (spec Ch.12 "Create"). <paramref name="types"/> pins explicit type membership; omitted, membership
    /// falls back to the input frontmatter's own explicit type key(s), then inferred matching — the
    /// same precedence a read uses. <paramref name="path"/> pins the target path explicitly; omitted, it
    /// is generated from the matched types' `collection.path.pattern` against the post-lifecycle draft.
    /// </summary>
    /// <exception cref="MdbWriteException">Every hard failure — schema validation, `type_membership_changed`, `unique_conflict`, path errors, `lifecycle_expression_error`, `path_conflict`.</exception>
    public MdbRecord Create(OrderedDictionary frontmatter, string? body = null, IReadOnlyList<string>? types = null, string? path = null, bool dryRun = false)
    {
        var pre = PreflightCreate(MdbBatchOperation.Create(frontmatter, body, types, path), _recordsByPath);
        if (dryRun)
        {
            return pre.Record!;
        }

        PersistAndRefresh(pre.Path, pre.Document!);
        return _recordsByPath[pre.Path];
    }

    /// <summary>
    /// Modifies an existing record (spec Ch.12 "Update"). Either a structured <paramref name="patch"/>
    /// (set/null only present keys) plus a separate <paramref name="remove"/> key list, or a complete
    /// replacement <paramref name="document"/> — never both. A document replacement that lifecycle
    /// leaves unaltered persists the caller's exact supplied bytes.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="document"/> combined with <paramref name="patch"/>/<paramref name="remove"/>/<paramref name="body"/>.</exception>
    /// <exception cref="MdbWriteException">Every hard failure — `record_not_found`, `concurrent_modification`, schema validation, `type_membership_changed`, `unique_conflict`, `lifecycle_expression_error`.</exception>
    public MdbRecord Update(
        string path, OrderedDictionary? patch = null, IReadOnlyList<string>? remove = null,
        string? body = null, string? document = null, string? ifRevision = null, bool dryRun = false)
    {
        var pre = PreflightUpdate(MdbBatchOperation.Update(path, patch, remove, body, document, ifRevision), _recordsByPath);
        if (dryRun)
        {
            return pre.Record!;
        }

        PersistAndRefresh(pre.Path, pre.Document!);
        return _recordsByPath[pre.Path];
    }

    /// <summary>Removes a record (spec Ch.12 "Delete") and patches the in-memory index via <see cref="Refresh"/>. Returns the record that was (or would be) deleted.</summary>
    /// <exception cref="MdbWriteException">`record_not_found`, `concurrent_modification`.</exception>
    public MdbRecord Delete(string path, string? ifRevision = null, bool dryRun = false)
    {
        var pre = PreflightDelete(MdbBatchOperation.Delete(path, ifRevision), _recordsByPath);
        if (dryRun)
        {
            return pre.Record!;
        }

        PersistPreflight(pre);
        return pre.Record!;
    }

    /// <summary>Moves a record to a new collection-relative path (spec Ch.12 "Rename"). Does not rewrite link text in any other record.</summary>
    /// <exception cref="MdbWriteException">`record_not_found`, `concurrent_modification`, `path_conflict`, a path escaping the collection root.</exception>
    public MdbRecord Rename(string path, string newPath, string? ifRevision = null, bool dryRun = false)
    {
        var pre = PreflightRename(MdbBatchOperation.Rename(path, newPath, ifRevision), _recordsByPath);
        if (dryRun)
        {
            return pre.Record!;
        }

        PersistPreflight(pre);
        return _recordsByPath[pre.Path];
    }

    /// <summary>
    /// Runs an ordered batch of create/update/delete/rename descriptors (spec Ch.12 "Batch"; #41
    /// point 33). By default (<paramref name="allowPartial"/> false) every operation is validated —
    /// each already-validated operation's effect visible to the next operation's uniqueness/path
    /// checks within the same batch — before any of them persists; the whole batch aborts on the
    /// first invalid operation. <paramref name="allowPartial"/> true instead validates-and-writes
    /// each operation independently, continuing past individual failures. Never throws for a
    /// per-operation failure — that's what the returned envelope is for.
    /// </summary>
    public IReadOnlyList<MdbBatchOperationResult> ExecuteBatch(IReadOnlyList<MdbBatchOperation> operations, bool allowPartial = false)
    {
        var results = new List<MdbBatchOperationResult>(operations.Count);

        if (allowPartial)
        {
            foreach (var op in operations)
            {
                try
                {
                    var pre = PreflightOperation(op, _recordsByPath);
                    PersistPreflight(pre);
                    var final = pre.Kind == MdbBatchOperationKind.Delete ? null : (_recordsByPath.TryGetValue(pre.Path, out var updated) ? updated : pre.Record);
                    results.Add(new MdbBatchOperationResult { Valid = true, Path = pre.Path, Result = final, Diagnostics = Array.Empty<MdbDiagnostic>() });
                }
                catch (MdbWriteException ex)
                {
                    results.Add(new MdbBatchOperationResult { Valid = false, Path = op.Path, Result = null, Diagnostics = new[] { ex.Diagnostic } });
                }
            }

            return results;
        }

        var overlay = new Dictionary<string, MdbRecord>(_recordsByPath, StringComparer.Ordinal);
        var preflights = new WritePreflightResult?[operations.Count];
        for (var i = 0; i < operations.Count; i++)
        {
            try
            {
                var pre = PreflightOperation(operations[i], overlay);
                preflights[i] = pre;
                ApplyOverlay(overlay, pre);
                results.Add(new MdbBatchOperationResult { Valid = true, Path = pre.Path, Result = pre.Kind == MdbBatchOperationKind.Delete ? null : pre.Record, Diagnostics = Array.Empty<MdbDiagnostic>() });
            }
            catch (MdbWriteException ex)
            {
                results.Add(new MdbBatchOperationResult { Valid = false, Path = operations[i].Path, Result = null, Diagnostics = new[] { ex.Diagnostic } });
                for (var j = i + 1; j < operations.Count; j++)
                {
                    results.Add(new MdbBatchOperationResult { Valid = false, Path = operations[j].Path, Result = null, Diagnostics = Array.Empty<MdbDiagnostic>() });
                }

                return results;
            }
        }

        foreach (var pre in preflights)
        {
            PersistPreflight(pre!);
        }

        for (var i = 0; i < results.Count; i++)
        {
            if (results[i].Valid && preflights[i]!.Kind != MdbBatchOperationKind.Delete)
            {
                results[i] = results[i] with { Result = _recordsByPath.TryGetValue(preflights[i]!.Path, out var updated) ? updated : results[i].Result };
            }
        }

        return results;
    }

    private sealed record WritePreflightResult
    {
        public required MdbBatchOperationKind Kind { get; init; }

        /// <summary>Final path — the resolved Create path, the Update/Delete path, or the Rename destination.</summary>
        public required string Path { get; init; }

        /// <summary>Rename source path only.</summary>
        public string? OldPath { get; init; }

        /// <summary>The would-be/authoritative record; null only impossible — Delete still returns the pre-delete record.</summary>
        public MdbRecord? Record { get; init; }

        /// <summary>Exact Markdown text to persist; null for Delete/Rename, which move/remove bytes rather than rewrite them.</summary>
        public string? Document { get; init; }
    }

    private WritePreflightResult PreflightOperation(MdbBatchOperation op, IReadOnlyDictionary<string, MdbRecord> view) => op.Kind switch
    {
        MdbBatchOperationKind.Create => PreflightCreate(op, view),
        MdbBatchOperationKind.Update => PreflightUpdate(op, view),
        MdbBatchOperationKind.Delete => PreflightDelete(op, view),
        MdbBatchOperationKind.Rename => PreflightRename(op, view),
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };

    private WritePreflightResult PreflightCreate(MdbBatchOperation op, IReadOnlyDictionary<string, MdbRecord> view)
    {
        if (op.Frontmatter is null)
        {
            throw new ArgumentException("Create requires frontmatter.", nameof(op));
        }

        var inputFrontmatter = op.Frontmatter;
        var inputBody = op.Body ?? string.Empty;
        var explicitPath = op.Path is null ? null : NormalizeCallerPath(op.Path);
        var diagnosticsHint = explicitPath ?? "(new record)";

        var draft = MdbWriteDraft.ForCreate(inputFrontmatter);
        var file = explicitPath is not null ? MdbFileCel.Build(RootPath, explicitPath, inputBody) : null;
        var matchedTypes = RunLifecycleAndValidate(op.Types, draft, isCreate: true, MdbLifecycleOperation.Create, file, diagnosticsHint, inputBody);

        CheckUniqueness(matchedTypes, draft.Fields, excludePath: null, view, diagnosticsHint);

        var resolvedPath = BoundaryCheckedRelativePath(ResolveCreatePath(matchedTypes, explicitPath, draft.Fields, diagnosticsHint), diagnosticsHint);
        if (view.ContainsKey(resolvedPath) || File.Exists(Path.Combine(RootPath, resolvedPath)))
        {
            throw new MdbWriteException(PathConflict(resolvedPath));
        }

        var document = FrontmatterWriter.Render(draft.Fields, inputBody);
        var revision = ComputeRevision(document);
        var record = RecordLoader.Load(resolvedPath, draft.Fields, inputBody, revision, matchedTypes, RootPath);

        return new WritePreflightResult { Kind = MdbBatchOperationKind.Create, Path = resolvedPath, Record = record, Document = document };
    }

    private WritePreflightResult PreflightUpdate(MdbBatchOperation op, IReadOnlyDictionary<string, MdbRecord> view)
    {
        if (op.Path is null)
        {
            throw new ArgumentException("Update requires a path.", nameof(op));
        }

        if (op.Document is not null && (op.Patch is not null || op.Remove is not null || op.Body is not null))
        {
            throw new ArgumentException("A document replacement cannot be combined with patch, remove, or body.", nameof(op));
        }

        var relativePath = BoundaryCheckedRelativePath(NormalizeCallerPath(op.Path), op.Path);
        var (currentFrontmatter, currentBody, currentRevision, existsOnDisk) = GetCurrentContent(relativePath, view);
        CheckIfRevision(op.IfRevision, existsOnDisk, currentRevision, relativePath);

        OrderedDictionary patchedFields;
        string newBody;
        string? exactSourceIfUnchanged = null;
        var isDocumentReplacement = op.Document is not null;
        if (isDocumentReplacement)
        {
            ParsedDocument replacementParsed;
            try
            {
                replacementParsed = FrontmatterParser.Parse(op.Document!);
            }
            catch (FrontmatterParseException ex)
            {
                throw new MdbWriteException(new MdbDiagnostic { Severity = MdbSeverity.Error, Code = "frontmatter_invalid", Message = ex.Message, Path = relativePath });
            }

            patchedFields = replacementParsed.Frontmatter;
            newBody = replacementParsed.Body;
            exactSourceIfUnchanged = op.Document;
        }
        else
        {
            patchedFields = ApplyPatch(currentFrontmatter, op.Patch, op.Remove);
            newBody = op.Body ?? currentBody;
        }

        var draft = MdbWriteDraft.ForUpdate(patchedFields, currentFrontmatter);
        var file = MdbFileCel.Build(RootPath, relativePath, newBody);
        var matchedTypes = RunLifecycleAndValidate(null, draft, isCreate: false, MdbLifecycleOperation.Update, file, relativePath, newBody);

        CheckUniqueness(matchedTypes, draft.Fields, excludePath: relativePath, view, relativePath);

        var lifecycleChangedFields = !JsonModel.DeepEquals(patchedFields, draft.Fields);
        var document = isDocumentReplacement && !lifecycleChangedFields ? exactSourceIfUnchanged! : FrontmatterWriter.Render(draft.Fields, newBody);
        var revision = ComputeRevision(document);
        var record = RecordLoader.Load(relativePath, draft.Fields, newBody, revision, matchedTypes, RootPath);

        return new WritePreflightResult { Kind = MdbBatchOperationKind.Update, Path = relativePath, Record = record, Document = document };
    }

    private WritePreflightResult PreflightDelete(MdbBatchOperation op, IReadOnlyDictionary<string, MdbRecord> view)
    {
        if (op.Path is null)
        {
            throw new ArgumentException("Delete requires a path.", nameof(op));
        }

        var relativePath = BoundaryCheckedRelativePath(NormalizeCallerPath(op.Path), op.Path);
        var (frontmatter, body, revision, existsOnDisk) = GetCurrentContent(relativePath, view);
        CheckIfRevision(op.IfRevision, existsOnDisk, revision, relativePath);

        var matchedTypes = ResolveTypesForWrite(null, frontmatter, relativePath, body);
        var effectiveRevision = revision ?? ComputeRevision(FrontmatterWriter.Render(frontmatter, body));
        var record = RecordLoader.Load(relativePath, frontmatter, body, effectiveRevision, matchedTypes, RootPath);

        return new WritePreflightResult { Kind = MdbBatchOperationKind.Delete, Path = relativePath, Record = record, Document = null };
    }

    private WritePreflightResult PreflightRename(MdbBatchOperation op, IReadOnlyDictionary<string, MdbRecord> view)
    {
        if (op.Path is null || op.NewPath is null)
        {
            throw new ArgumentException("Rename requires path and newPath.", nameof(op));
        }

        var relativePath = BoundaryCheckedRelativePath(NormalizeCallerPath(op.Path), op.Path);
        var (frontmatter, body, revision, existsOnDisk) = GetCurrentContent(relativePath, view);
        CheckIfRevision(op.IfRevision, existsOnDisk, revision, relativePath);

        var newRelativePath = BoundaryCheckedRelativePath(NormalizeCallerPath(op.NewPath), relativePath);
        if (view.ContainsKey(newRelativePath) || File.Exists(Path.Combine(RootPath, newRelativePath)))
        {
            throw new MdbWriteException(PathConflict(newRelativePath));
        }

        var matchedTypes = ResolveTypesForWrite(null, frontmatter, newRelativePath, body);
        var effectiveRevision = revision ?? ComputeRevision(FrontmatterWriter.Render(frontmatter, body));
        var record = RecordLoader.Load(newRelativePath, frontmatter, body, effectiveRevision, matchedTypes, RootPath);

        return new WritePreflightResult { Kind = MdbBatchOperationKind.Rename, Path = newRelativePath, OldPath = relativePath, Record = record, Document = null };
    }

    /// <summary>
    /// Runs Ch.12 Create/Update steps 3-6 uniformly: freeze pre-lifecycle membership, apply
    /// lifecycle, re-verify membership (`type_membership_changed` on drift), then validate JSON
    /// Schema (`RecordLoader.ValidateSchemas`'s own first diagnostic on failure). Shared by
    /// <see cref="PreflightCreate"/> and <see cref="PreflightUpdate"/> — the only difference
    /// between the two operations at this stage is which type-resolution/lifecycle inputs the
    /// caller has already prepared.
    /// </summary>
    private IReadOnlyList<MdbType> RunLifecycleAndValidate(
        IReadOnlyList<string>? explicitTypes, MdbWriteDraft draft, bool isCreate, MdbLifecycleOperation operation, MdbFileCel? file, string diagnosticsHint, string body)
    {
        var preLifecycleTypes = ResolveTypesForWrite(explicitTypes, draft.Fields, diagnosticsHint, body);
        var preMembershipKey = MembershipKey(preLifecycleTypes);

        RunLifecycle(preLifecycleTypes, isCreate, draft, operation, file, diagnosticsHint);

        var postLifecycleTypes = ResolveTypesForWrite(explicitTypes, draft.Fields, diagnosticsHint, body);
        if (!string.Equals(MembershipKey(postLifecycleTypes), preMembershipKey, StringComparison.Ordinal))
        {
            throw new MdbWriteException(TypeMembershipChanged(diagnosticsHint));
        }

        var (isValid, validationDiagnostics) = RecordLoader.ValidateSchemas(draft.Fields, diagnosticsHint, postLifecycleTypes);
        if (!isValid)
        {
            throw new MdbWriteException(validationDiagnostics[0]);
        }

        return postLifecycleTypes;
    }

    /// <summary>Shared `if_revision` check for Update/Delete/Rename (spec Ch.12 "Concurrency"): fails with `concurrent_modification` when unset-on-disk or mismatched — never against a cached in-memory value.</summary>
    private static void CheckIfRevision(string? ifRevision, bool existsOnDisk, string? actualRevision, string relativePath)
    {
        if (ifRevision is not null && (!existsOnDisk || !string.Equals(ifRevision, actualRevision, StringComparison.Ordinal)))
        {
            throw new MdbWriteException(ConcurrentModification(relativePath));
        }
    }

    private void PersistPreflight(WritePreflightResult pre)
    {
        switch (pre.Kind)
        {
            case MdbBatchOperationKind.Delete:
                File.Delete(Path.Combine(RootPath, pre.Path));
                Refresh(pre.Path);
                break;
            case MdbBatchOperationKind.Rename:
                var oldAbsolute = Path.Combine(RootPath, pre.OldPath!);
                var newAbsolute = Path.Combine(RootPath, pre.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(newAbsolute)!);
                File.Move(oldAbsolute, newAbsolute);
                Refresh(pre.OldPath!);
                Refresh(pre.Path);
                break;
            default:
                PersistAndRefresh(pre.Path, pre.Document!);
                break;
        }
    }

    private static void ApplyOverlay(Dictionary<string, MdbRecord> overlay, WritePreflightResult pre)
    {
        switch (pre.Kind)
        {
            case MdbBatchOperationKind.Delete:
                overlay.Remove(pre.Path);
                break;
            case MdbBatchOperationKind.Rename:
                overlay.Remove(pre.OldPath!);
                overlay[pre.Path] = pre.Record!;
                break;
            default:
                overlay[pre.Path] = pre.Record!;
                break;
        }
    }

    /// <summary>Reads a record's current content: real persisted bytes when the file exists on disk, otherwise a same-batch overlay entry (a not-yet-persisted create earlier in this batch). Throws `record_not_found` when neither has it.</summary>
    private (OrderedDictionary Frontmatter, string Body, string? Revision, bool ExistsOnDisk) GetCurrentContent(string relativePath, IReadOnlyDictionary<string, MdbRecord> view)
    {
        var absolutePath = Path.Combine(RootPath, relativePath);
        if (File.Exists(absolutePath))
        {
            var bytes = File.ReadAllBytes(absolutePath);
            var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(StripUtf8Bom(bytes));
            var parsed = FrontmatterParser.Parse(text);
            return (parsed.Frontmatter, parsed.Body, ComputeRevision(bytes), true);
        }

        if (view.TryGetValue(relativePath, out var overlayRecord))
        {
            return (overlayRecord.Frontmatter, overlayRecord.Body, null, false);
        }

        throw new MdbWriteException(NotFound(relativePath));
    }

    /// <summary>Applies a structured patch (spec Ch.12 "Update"): a present key sets or nulls that field; <paramref name="remove"/> deletes a key outright; every other key stays untouched.</summary>
    private static OrderedDictionary ApplyPatch(OrderedDictionary current, OrderedDictionary? patch, IReadOnlyList<string>? remove)
    {
        var result = MdbWriteDraft.Clone(current);
        if (patch is not null)
        {
            foreach (System.Collections.DictionaryEntry entry in patch)
            {
                result[(string)entry.Key] = entry.Value;
            }
        }

        if (remove is not null)
        {
            foreach (var key in remove)
            {
                result.Remove(key);
            }
        }

        return result;
    }

    /// <summary>
    /// Composes and executes one lifecycle event's actions against <paramref name="draft"/> (spec
    /// Ch.09). Cross-type conflict detection reuses <see cref="TypeConflictComposer"/> (#34); within
    /// one field's coalesced rule sequence, guarded actions run in declared order, later
    /// assignments overwriting earlier ones (#41 point 8).
    /// </summary>
    private void RunLifecycle(
        IReadOnlyList<MdbType> matchedTypes, bool isCreate, MdbWriteDraft draft, MdbLifecycleOperation operation, MdbFileCel? file, string diagnosticsHint)
    {
        Func<MdbType, IReadOnlyDictionary<string, IReadOnlyList<MdbLifecycleRule>>> selector =
            isCreate ? type => type.LifecycleOnCreate : type => type.LifecycleOnUpdate;

        var (coalesced, conflicts) = TypeConflictComposer.Compose(matchedTypes, selector, MdbLifecycleRuleListComparer.Instance, diagnosticsHint);
        if (conflicts.Count > 0)
        {
            throw new MdbWriteException(conflicts[0]);
        }

        var fieldOrder = new List<string>();
        var seenFields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in matchedTypes)
        {
            foreach (var field in selector(type).Keys)
            {
                if (seenFields.Add(field))
                {
                    fieldOrder.Add(field);
                }
            }
        }

        foreach (var field in fieldOrder)
        {
            if (!coalesced.TryGetValue(field, out var rules))
            {
                continue;
            }

            foreach (var rule in rules)
            {
                if (!LifecycleGuardEvaluator.ShouldRun(rule, draft.Fields, draft.Old, operation, file, diagnosticsHint))
                {
                    continue;
                }

                draft.Fields[rule.Field] = LifecycleProviders.Evaluate(rule.ProviderKind, rule.ProviderArg, draft.Fields);
            }
        }
    }

    /// <summary>Evaluates every matched type's `collection.unique` rules against <paramref name="view"/> (spec Ch.07 "Cross-File Uniqueness"). Additive per declaring type — never composed for coalesce/conflict.</summary>
    private static void CheckUniqueness(
        IReadOnlyList<MdbType> matchedTypes, OrderedDictionary draftFields, string? excludePath, IReadOnlyDictionary<string, MdbRecord> view, string diagnosticsHint)
    {
        foreach (var type in matchedTypes)
        {
            foreach (var rule in type.Unique)
            {
                if (!draftFields.Contains(rule.Field) || draftFields[rule.Field] is null)
                {
                    continue;
                }

                var value = draftFields[rule.Field];
                foreach (var (candidatePath, candidate) in view)
                {
                    if (excludePath is not null && string.Equals(candidatePath, excludePath, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!InUniqueScope(rule, type, candidatePath, candidate))
                    {
                        continue;
                    }

                    if (!candidate.Frontmatter.Contains(rule.Field) || candidate.Frontmatter[rule.Field] is null)
                    {
                        continue;
                    }

                    if (!JsonModel.DeepEquals(candidate.Frontmatter[rule.Field], value))
                    {
                        continue;
                    }

                    throw new MdbWriteException(new MdbDiagnostic
                    {
                        Severity = MdbSeverity.Error,
                        Code = "unique_conflict",
                        Message = $"Field '{rule.Field}' on type '{type.Name}' must be unique ({rule.Scope.ToString().ToLowerInvariant()} scope); '{candidatePath}' already has the same value.",
                        Path = diagnosticsHint,
                        Field = rule.Field,
                        Type = type.Name,
                        Details = new Dictionary<string, object?> { ["conflicting_path"] = candidatePath },
                    });
                }
            }
        }
    }

    private static bool InUniqueScope(MdbUniqueRule rule, MdbType type, string candidatePath, MdbRecord candidate) => rule.Scope switch
    {
        MdbUniqueScope.Collection => true,
        MdbUniqueScope.Type => candidate.MatchedTypes.Any(t => ReferenceEquals(t, type) || t.CanonicalName == type.CanonicalName),
        MdbUniqueScope.PathGlob => rule.CompiledPathGlob!.IsMatch(candidatePath),
        _ => false,
    };

    /// <summary>Resolves a Create target path (spec Ch.07 "Path Policy"; Ch.12 step 8): explicit wins; otherwise composes+generates from `collection.path.pattern` against the post-lifecycle draft.</summary>
    private static string ResolveCreatePath(IReadOnlyList<MdbType> matchedTypes, string? explicitPath, OrderedDictionary draftFields, string diagnosticsHint)
    {
        if (explicitPath is not null)
        {
            return explicitPath;
        }

        var patternsByType = matchedTypes.Where(t => t.PathPattern is not null).ToArray();
        if (patternsByType.Length == 0)
        {
            throw new MdbWriteException(new MdbDiagnostic
            {
                Severity = MdbSeverity.Error,
                Code = "no_policy_available",
                Message = "No explicit path was given and no matched type declares a 'collection.path.pattern'.",
                Path = diagnosticsHint,
            });
        }

        var distinctPatterns = patternsByType.Select(t => t.PathPattern!.Source).Distinct(StringComparer.Ordinal).ToArray();
        if (distinctPatterns.Length > 1)
        {
            throw new MdbWriteException(new MdbDiagnostic
            {
                Severity = MdbSeverity.Error,
                Code = "type_conflict",
                Message = $"Matched types declare conflicting 'collection.path.pattern' values: {string.Join(", ", distinctPatterns)}.",
                Path = diagnosticsHint,
                Details = new Dictionary<string, object?> { ["types"] = patternsByType.Select(t => t.Name).ToArray() },
            });
        }

        var generated = patternsByType[0].PathPattern!.Generate(draftFields, out var missingField, out var invalidField, out var invalidValue);
        if (generated is not null)
        {
            return generated;
        }

        if (missingField is not null)
        {
            throw new MdbWriteException(new MdbDiagnostic
            {
                Severity = MdbSeverity.Error,
                Code = "path_value_missing",
                Message = $"Path pattern placeholder '{{{missingField}}}' is missing or null.",
                Path = diagnosticsHint,
                Field = missingField,
            });
        }

        throw new MdbWriteException(new MdbDiagnostic
        {
            Severity = MdbSeverity.Error,
            Code = "invalid_path_component",
            Message = $"Path pattern placeholder '{{{invalidField}}}' produced an invalid path component '{invalidValue}'.",
            Path = diagnosticsHint,
            Field = invalidField,
        });
    }

    /// <summary>Rejects `..`/`.` traversal segments and a symlink escape (mirroring the existing Phase-1/2 discovery boundary check) — a generated or explicit target path must stay inside the collection root.</summary>
    private string BoundaryCheckedRelativePath(string relativePath, string diagnosticsHint)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        if (normalized.Length == 0 || normalized.Split('/').Any(segment => segment is "." or ".."))
        {
            throw Escape();
        }

        var rootReal = ResolveRealPath(RootPath);
        var absolute = Path.Combine(RootPath, normalized);
        var probe = Path.GetDirectoryName(absolute) ?? string.Empty;
        while (probe.Length > 0 && !Directory.Exists(probe))
        {
            probe = Path.GetDirectoryName(probe) ?? string.Empty;
        }

        var probeReal = probe.Length == 0 ? rootReal : ResolveRealPath(probe);
        if (!string.Equals(probeReal, rootReal, StringComparison.Ordinal) && !probeReal.StartsWith(rootReal + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw Escape();
        }

        return normalized;

        MdbWriteException Escape() => new(new MdbDiagnostic
        {
            Severity = MdbSeverity.Error,
            Code = "path_traversal",
            Message = $"Path '{relativePath}' escapes the collection root.",
            Path = diagnosticsHint,
        });
    }

    private static string ResolveRealPath(string path)
    {
        var full = Path.GetFullPath(path);
        try
        {
            return new DirectoryInfo(full).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? full;
        }
        catch (IOException)
        {
            return full;
        }
    }

    private void PersistAndRefresh(string relativePath, string document)
    {
        var absolutePath = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        AtomicWrite(absolutePath, document);
        Refresh(relativePath);
    }

    /// <summary>Write-to-temp-then-move within the same directory (spec Ch.12 "Core Write Requirements") — a crash mid-write never leaves a half-written frontmatter block.</summary>
    private static void AtomicWrite(string absolutePath, string content)
    {
        var directory = Path.GetDirectoryName(absolutePath)!;
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(absolutePath)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(tempPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(tempPath, absolutePath, overwrite: true);
    }

    private static string ComputeRevision(string text) => ComputeRevision(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text));

    private static string ComputeRevision(byte[] bytes) =>
        "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

    private static string NormalizeCallerPath(string path) => path.Replace('\\', '/').TrimStart('/');

    /// <summary>An ordered fingerprint of a matched-type set, used to detect `type_membership_changed` (spec Ch.05 "Write-Time Type Membership").</summary>
    private static string MembershipKey(IReadOnlyList<MdbType> types) => string.Join('\u0001', types.Select(t => t.CanonicalName));

    private static MdbDiagnostic NotFound(string path) => new()
    {
        Severity = MdbSeverity.Error,
        Code = "record_not_found",
        Message = $"No record exists at '{path}'.",
        Path = path,
    };

    private static MdbDiagnostic ConcurrentModification(string path) => new()
    {
        Severity = MdbSeverity.Error,
        Code = "concurrent_modification",
        Message = $"The record at '{path}' was modified since the supplied revision.",
        Path = path,
    };

    private static MdbDiagnostic PathConflict(string path) => new()
    {
        Severity = MdbSeverity.Error,
        Code = "path_conflict",
        Message = $"A record already exists at '{path}'.",
        Path = path,
    };

    private static MdbDiagnostic TypeMembershipChanged(string path) => new()
    {
        Severity = MdbSeverity.Error,
        Code = "type_membership_changed",
        Message = $"Lifecycle changed the record's matched type membership for '{path}'.",
        Path = path,
    };
}
