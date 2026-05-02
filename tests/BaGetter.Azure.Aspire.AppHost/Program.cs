var builder = DistributedApplication.CreateBuilder(args);

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();

var blobs = storage.AddBlobs("blobs");
var tables = storage.AddTables("tables");
var packageContainer = storage.AddBlobContainer("package-container", "packages");

builder.AddProject<Projects.BaGetter>("bagetter")
    .WithEnvironment("Database__Type", "AzureTable")
    .WithEnvironment("Database__ConnectionString", tables.Resource.ConnectionStringExpression)
    .WithEnvironment("Database__TableName", "packages")
    .WithEnvironment("Storage__Type", "AzureBlobStorage")
    .WithEnvironment("Storage__ConnectionString", blobs.Resource.ConnectionStringExpression)
    .WithEnvironment("Storage__Container", "packages")
    .WithEnvironment("Search__Type", "Database")
    .WaitFor(blobs)
    .WaitFor(packageContainer)
    .WaitFor(tables);

await builder.Build().RunAsync();
