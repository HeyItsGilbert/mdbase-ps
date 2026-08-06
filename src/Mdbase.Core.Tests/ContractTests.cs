using Mdbase.Core.Tests.Fixtures;

namespace Mdbase.Core.Tests;

public class ContractTests
{
    [Fact]
    public void Connect_loads_contract_and_validated_type_implementation()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_contracts/task.md", RecordContract());
        fixture.WriteFile("_types/task.md", TaskType());
        fixture.WriteFile("tasks/a.md", "---\ntype: task\ntitle: Ship it\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);

        var contract = Assert.Single(collection.Contracts).Value;
        Assert.Equal(ContractType.Record, contract.ContractType);
        Assert.StartsWith("sha256:", contract.Digest);
        var type = Assert.Single(collection.Types).Value;
        var implementation = Assert.Single(type.Implements);
        Assert.Equal(contract.Digest, implementation.ContractDigest);
        Assert.Equal(new[] { type }, collection.GetImplementations("example.task", "1.0.0"));
        Assert.Empty(collection.Diagnostics);
    }

    [Fact]
    public void Connect_loads_unnamed_contract_and_orders_implementations_canonically()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_contracts/note.md", """
            ---
            kind: mdbase.contract
            contract_type: record
            id: example.note
            version: "1.0.0"
            record_schema:
              dialect: json-schema-2020-12
              value:
                type: object
                required: [title]
                properties:
                  title: { type: string }
            ---
            """);
        fixture.WriteFile("_types/zulu.md", """
            ---
            kind: mdbase.type
            name: zulu_note
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
                properties:
                  title: { type: string }
            implements:
              - contract: example.note
                version: "1.0.0"
                fields:
                  title: title
            ---
            """);
        fixture.WriteFile("_types/alpha.md", """
            ---
            kind: mdbase.type
            name: alpha_note
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
                properties:
                  title: { type: string }
            implements:
              - contract: example.note
                version: "1.0.0"
                fields:
                  title: title
            ---
            """);

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Single(collection.Contracts);
        Assert.Null(collection.Contracts[("example.note", "1.0.0")].Name);
        Assert.Equal(new[] { "alpha_note", "zulu_note" }, collection.GetImplementations("example.note", "1.0.0").Select(type => type.Name));
        Assert.Empty(collection.Diagnostics);
    }

    [Fact]
    public void Connect_rejects_dangling_contract_implementation()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/task.md", TaskType("example.missing"));

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Empty(collection.Types);
        Assert.Contains(collection.Diagnostics, diagnostic => diagnostic.Code == "data_contract_not_found");
    }

    [Fact]
    public void Connect_loads_event_and_action_contracts()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_contracts/event.md", """
            ---
            kind: mdbase.contract
            id: example.changed
            version: "1.0.0"
            name: Changed
            contract_type: event
            data_schema:
              dialect: json-schema-2020-12
              value: { type: object }
            ---
            """);
        fixture.WriteFile("_contracts/action.md", """
            ---
            kind: mdbase.contract
            id: example.archive
            version: "1.0.0"
            name: Archive
            contract_type: action
            input_schema:
              dialect: json-schema-2020-12
              value: { type: object }
            ---
            """);

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Equal(ContractType.Event, collection.Contracts[("example.changed", "1.0.0")].ContractType);
        Assert.NotNull(collection.Contracts[("example.changed", "1.0.0")].DataSchema);
        Assert.Equal(ContractType.Action, collection.Contracts[("example.archive", "1.0.0")].ContractType);
        Assert.NotNull(collection.Contracts[("example.archive", "1.0.0")].InputSchema);
    }

    [Fact]
    public void Connect_preserves_raw_action_behavior()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_contracts/archive.md", """
            ---
            kind: mdbase.contract
            id: example.archive
            version: "1.0.0"
            contract_type: action
            input_schema:
              dialect: json-schema-2020-12
              value: { type: object }
            behavior:
              custom_policy: retain
            ---
            """);

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Equal("retain", collection.Contracts[("example.archive", "1.0.0")].Behavior?["custom_policy"]);
        Assert.Empty(collection.Diagnostics);
    }

    [Fact]
    public void Connect_treats_null_implementation_binding_as_absent()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_contracts/task.md", RecordContract());
        fixture.WriteFile("_types/task.md", """
            ---
            kind: mdbase.type
            name: task
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
                properties:
                  title: { type: string }
            implements:
              - contract: example.task
                version: "1.0.0"
                fields:
                  title: title
                binding: null
            ---
            """);

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Single(collection.Types);
        Assert.Null(Assert.Single(collection.Types).Value.Implements.Single().Binding);
        Assert.Empty(collection.Diagnostics);
    }

    [Fact]
    public void GetContractView_projects_effective_frontmatter_and_validates_it()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_contracts/task.md", RecordContract());
        fixture.WriteFile("_types/task.md", TaskType());
        fixture.WriteFile("tasks/a.md", "---\ntype: task\ntitle: Ship it\n---\n");
        var collection = MdbCollection.Connect(fixture.RootPath);
        var record = collection.Records["tasks/a.md"];
        var type = collection.Types["task"];

        var result = collection.GetContractView(record, type, "example.task", "1.0.0");

        Assert.Equal("Ship it", result.View["title"]!.GetValue<string>());
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public void GetContractView_returns_a_diagnostic_when_the_projected_view_is_invalid()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_contracts/task.md", RecordContract());
        fixture.WriteFile("_types/integer-task.md", """
            ---
            kind: mdbase.type
            name: integer_task
            schema:
              dialect: json-schema-2020-12
              value:
                type: object
                required: [title]
                properties:
                  title: { type: integer }
            implements:
              - contract: example.task
                version: "1.0.0"
                fields:
                  title: title
            ---
            """);
        fixture.WriteFile("tasks/a.md", "---\ntype: integer_task\ntitle: 7\n---\n");

        var collection = MdbCollection.Connect(fixture.RootPath);
        var result = collection.GetContractView(collection.Records["tasks/a.md"], collection.Types["integer_task"], "example.task", "1.0.0");
        Assert.NotNull(result.Diagnostic);
        Assert.Equal("data_contract_record_invalid", result.Diagnostic?.Code);
    }

    [Fact]
    public void Refreshing_contract_path_revalidates_type_claims()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_contracts/task.md", RecordContract());
        fixture.WriteFile("_types/task.md", TaskType());
        var collection = MdbCollection.Connect(fixture.RootPath);
        Assert.Single(collection.Types);

        fixture.WriteFile("_contracts/task.md", RecordContract(requiredField: "summary"));
        collection.Refresh("_contracts/task.md");

        Assert.Empty(collection.Types);
        Assert.Contains(collection.Diagnostics, diagnostic => diagnostic.Code == "data_contract_field_invalid");
    }

    private static string RecordContract(string requiredField = "title") => $$"""
        ---
        kind: mdbase.contract
        id: example.task
        version: "1.0.0"
        name: Task
        contract_type: record
        record_schema:
          dialect: json-schema-2020-12
          value:
            type: object
            required: [{{requiredField}}]
            properties:
              title: { type: string }
              summary: { type: string }
        ---
        """;

    private static string TaskType(string contract = "example.task") => $$"""
        ---
        kind: mdbase.type
        name: task
        schema:
          dialect: json-schema-2020-12
          value:
            type: object
            properties:
              title: { type: string }
        implements:
          - contract: {{contract}}
            version: "1.0.0"
            fields:
              title: title
        ---
        """;
}
