Migration Plan for DynamoDbMaterialRepository

Objective
The goal is to refactor the DynamoDbMaterialRepository class to eliminate the dependency on the obsolete IDynamoDBContext. All data access methods will be rewritten to use the modern, low-level IAmazonDynamoDBv2 client, which is the recommended approach for the AWS SDK for .NET V4.

Step 1: Remove IDynamoDBContext from Dependency Injection
First, clean up the class constructor. You only need the IAmazonDynamoDBv2 client and your options configuration.

BEFORE (V3):
```csharp
public sealed class DynamoDbMaterialRepository(IDynamoDBContext context, IAmazonDynamoDB client, IOptions<MaterialsOptions> options) : IMaterialRepository
{
    private readonly MaterialsOptions _options = options.Value;
    private readonly DynamoDBOperationConfig _dynamoConfig = new() { OverrideTableName = options.Value.TableName };
    // ...
}
```

AFTER (V4):
Remember to use the correct V4 interface, IAmazonDynamoDBv2.
```csharp
public sealed class DynamoDbMaterialRepository(IAmazonDynamoDBv2 client, IOptions<MaterialsOptions> options) : IMaterialRepository
{
    private readonly MaterialsOptions _options = options.Value;
    // The _dynamoConfig field is no longer needed, as the table name will be passed directly in each request.
}
```

Step 2: Refactor the GetByIdAsync Method
This method used context.LoadAsync. We will replace it with client.GetItemAsync, which requires manually building a GetItemRequest and deserializing the response.

BEFORE (V3):
```csharp
public async Task<Result<MaterialFormulation>> GetByIdAsync(string materialId)
{
    try
    {
#pragma warning disable CS0618
        var entity = await context.LoadAsync<MaterialEntity>(materialId, _dynamoConfig);
#pragma warning restore CS0618
        return entity is null 
            ? Result.Failure<MaterialFormulation>(Error.NotFound) 
            : Result.Success(MapToDomain(entity));
    }
    // ...
}
```

AFTER (V4):
```csharp
public async Task<Result<MaterialFormulation>> GetByIdAsync(string materialId)
{
    try
    {
        var request = new GetItemRequest
        {
            TableName = _options.TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "material_id", new AttributeValue { S = materialId } }
            }
        };

        var response = await client.GetItemAsync(request);

        if (response.Item == null || response.Item.Count == 0)
        {
            return Result.Failure<MaterialFormulation>(Error.NotFound);
        }

        // We use the Document model to easily convert the attribute map to a JSON string
        var itemDocument = Document.FromAttributeMap(response.Item);
        var entity = JsonSerializer.Deserialize<MaterialEntity>(itemDocument.ToJson());
        
        return Result.Success(MapToDomain(entity!));
    }
    catch (Exception ex)
    {
        return Result.Failure<MaterialFormulation>(new Error("DynamoDb.Error", ex.Message));
    }
}
```
> **Note:** For `JsonSerializer.Deserialize`, you will need to add `using System.Text.Json;`. Ensure the primary key field name (`"material_id"`) matches your `MaterialEntity` definition.

---

### Step 3: Refactor the `SaveAsync` Method

This method used `context.SaveAsync`. We will replace it with `client.PutItemAsync`. This involves serializing your object to a DynamoDB `Document` and then to an attribute map.

**BEFORE (V3):**
```csharp
public async Task<Result> SaveAsync(MaterialFormulation material)
{
    try
    {
        var entity = MapToEntity(material);
#pragma warning disable CS0618
        await context.SaveAsync(entity, _dynamoConfig);
#pragma warning restore CS0618
        return Result.Success();
    }
    // ...
}
```

**AFTER (V4):**
```csharp
public async Task<Result> SaveAsync(MaterialFormulation material)
{
    try
    {
        var entity = MapToEntity(material);

        // Serialize the entity to a JSON string, then to a DynamoDB Document
        var json = JsonSerializer.Serialize(entity);
        var itemDocument = Document.FromJson(json);

        var request = new PutItemRequest
        {
            TableName = _options.TableName,
            Item = itemDocument.ToAttributeMap()
        };

        await client.PutItemAsync(request);
        
        return Result.Success();
    }
    catch (Exception ex)
    {
        return Result.Failure(new Error("DynamoDb.Error", ex.Message));
    }
}
```

Step 4: Refactor the DeleteAsync Method
This method used context.DeleteAsync. We will replace it with client.DeleteItemAsync.

BEFORE (V3):
```csharp
public async Task<Result> DeleteAsync(string materialId)
{
    try
    {
#pragma warning disable CS0618
        await context.DeleteAsync<MaterialEntity>(materialId, _dynamoConfig);
#pragma warning restore CS0618
        return Result.Success();
    }
    // ...
}
```

AFTER (V4):
```csharp
public async Task<Result> DeleteAsync(string materialId)
{
    try
    {
        var request = new DeleteItemRequest
        {
            TableName = _options.TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "material_id", new AttributeValue { S = materialId } }
            }
        };

        await client.DeleteItemAsync(request);

        return Result.Success();
    }
    catch (Exception ex)
    {
        return Result.Failure(new Error("DynamoDb.Error", ex.Message));
    }
}
```

Step 5: Adjust Methods That Indirectly Used IDynamoDBContext
Your GetAllAsync and GetUniqueComponentsAsync methods used context.FromDocument for deserialization. Let's replace it with JsonSerializer for consistency.

In GetAllAsync:
```csharp
// BEFORE
items.AddRange(dynamoItems
    .Select(item => context.FromDocument<MaterialEntity>(Document.FromAttributeMap(item)))
    // ...

// AFTER
items.AddRange(dynamoItems
    .Select(item => Document.FromAttributeMap(item).ToJson())
    .Select(json => JsonSerializer.Deserialize<MaterialEntity>(json!))
    // ...
```

In GetUniqueComponentsAsync:
```csharp
// BEFORE
var entity = context.FromDocument<MaterialEntity>(doc);

// AFTER
var json = doc.ToJson();
var entity = JsonSerializer.Deserialize<MaterialEntity>(json!);
```

Summary and Final Checklist
Clean up the Constructor: Remove the IDynamoDBContext injection and the private _dynamoConfig field.
Update GetByIdAsync: Replace context.LoadAsync with client.GetItemAsync, build the GetItemRequest, and handle the case where no item is found.
Update SaveAsync: Replace context.SaveAsync with client.PutItemAsync. Serialize your entity object to an attribute map for the request.
Update DeleteAsync: Replace context.DeleteAsync with client.DeleteItemAsync, building the DeleteItemRequest with the primary key.
Standardize Deserialization: Replace all context.FromDocument<T>() calls with JsonSerializer.Deserialize<T>() after converting the Document to a JSON string via .ToJson().
Add using Statement: Ensure you have using System.Text.Json; at the top of your file.

By following this plan, your DynamoDbMaterialRepository will be fully migrated to the modern AWS SDK V4, resulting in cleaner, more performant code that is free of obsolete warnings and ready for the future.

## Verification Status (2026-02-22)

**Status: COMPLETE**

The migration to AWS SDK V4 (using `IAmazonDynamoDB` and `IAmazonDynamoDBv2` directly) has been verified across the codebase.
- **PoC.Materials:** `DynamoDbMaterialRepository` uses `IAmazonDynamoDB` and `QueryAsync`/`GetItemAsync`. No `IDynamoDBContext` usage found.
- **PoC.Costing:** `DynamoDbCostingRepository` uses `IAmazonDynamoDB` and `GetItemAsync`. No `IDynamoDBContext` usage found.
- **PoC.Populator:** Does not use DynamoDB directly (calls API).

No further action is required for this migration.
