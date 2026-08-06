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
        Assert.Equal(new[] { type }, collection.GetImplementations("task", "1.0.0"));
        Assert.Empty(collection.Diagnostics);
    }

    [Fact]
    public void Connect_rejects_dangling_contract_implementation()
    {
        using var fixture = new TempCollection();
        fixture.WriteFile("_types/task.md", TaskType("missing"));

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
            id: changed
            version: "1.0.0"
            name: Changed
            contract_type: event
            data_schema:
              value: { type: object }
            ---
            """);
        fixture.WriteFile("_contracts/action.md", """
            ---
            kind: mdbase.contract
            id: archive
            version: "1.0.0"
            name: Archive
            contract_type: action
            input_schema:
              value: { type: object }
            ---
            """);

        var collection = MdbCollection.Connect(fixture.RootPath);

        Assert.Equal(ContractType.Event, collection.Contracts[("changed", "1.0.0")].ContractType);
        Assert.NotNull(collection.Contracts[("changed", "1.0.0")].DataSchema);
        Assert.Equal(ContractType.Action, collection.Contracts[("archive", "1.0.0")].ContractType);
        Assert.NotNull(collection.Contracts[("archive", "1.0.0")].InputSchema);
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

        var result = collection.GetContractView(record, type, "task", "1.0.0");

        Assert.Equal("Ship it", result.View["title"]!.GetValue<string>());
        Assert.Null(result.Diagnostic);
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
        id: task
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

    private static string TaskType(string contract = "task") => $$"""
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
