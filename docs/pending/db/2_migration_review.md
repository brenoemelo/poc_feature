# Code Review: DynamoDB Migration (V4 SDK)

## 1. Implementation Status (Complete ✅)
All steps described in the migration plan `db_migration.md` have been successfully completed.

- The obsolete `IDynamoDBContext` has been completely removed from all constructors and dependency injections.

- The new low-level client `IAmazonDynamoDB` is being used correctly with `GetItemAsync`, `PutItemAsync`, `DeleteItemRequest`, and `QueryAsync`/`ScanAsync`.

- The strategy described in the serialization document using `Document.FromJson()` -> `ToAttributeMap()` and `Document.FromAttributeMap()` -> `ToJson()` -> `JsonSerializer` was implemented meticulously in both persistence projects (`DynamoDbMaterialRepository.cs` and `DynamoDbCostingRepository.cs`).

- **Pending Document Steps:** None. The migration is 100% complete from the perspective of `db_migration.md`.

---

## 2. Critical Issues and Resilience (Hidden Bugs 🐛)

I found the following production risks that will break your API under moderate workload:

* **Hard Limit on `BatchGetItemAsync` (CostingRepository):**

In the `GetPricesAsync` method, the `BatchGetItemRequest` request receives an array of `componentNames`. By design, the DynamoDB API rejects **any batch above 100 items**. If this endpoint is called by a giant recipe (Formulation) that has 101 distinct ingredients, it will throw a "ValidationException".

*Solution*: Chunk the list of `keys` into subgroups of 100 items maximum, loop `BatchGetItemAsync` (or run it in parallel with `Task.WhenAll`), and handle unprocessed keys (`UnprocessedKeys`) before grouping the result.

---

## 3. Cloud Bad Practices and Anti-Patterns ⚠️

* **Full Table Scans (`ScanAsync` no Costing):**

The `GetAllPricesAsync` and `GetPricesCountAsync` methods use `ScanRequest` without a filter, forcing DynamoDB to physically read **all items from the database**. This consumes massive amounts of RCUs (Read Capacity Units) as the table grows and is extremely slow.

*Solution*: Paginate GetAll and accept that returning the entire table does not scale. And for Count, DynamoDB provides methods via "DescribeTable" that return the approximate count of items without any computational cost. If you want a consistent, exact count, it is recommended to use the Atomic Counter technique.

* **`GetCountAsync` with Query (Materials):**
Although it uses a query on the index (much better than Scan), it still hits all pages to sum the count using O(N). An atomic counter would be a superior choice if this route is called frequently and there are thousands or millions of data points.

---

## 4. Performance Suggestions (Memory Evasion / GC) 🚀

* **Unnecessary Deserialization of Entire Classes:**
In `DynamoDbMaterialRepository.GetUniqueComponentsAsync`, you only want to list the `Components` of the formulas. The requests retrieve all the attributes from the table, and the code allocates a `Document` -> `JSON String` in memory, allocating and deserializing the entire `MaterialEntity` object. Then it discards the class and extracts the strings.

**Solution*: Extract the properties directly via `ProjectionExpression` in the DynamoDB `QueryRequest` to reduce network payload, and when reading from the low-level API, navigate the `item["formulation"].L` dictionary manually. This avoids megabytes of String allocation and GC.

**Overhead of the JSON <-> Document Bridge:**

The `.FromJson` -> `.ToAttributeMap` migration step works and is practical, but allocates a lot of extra memory. Direct mappings created manually (e.g., dictionary populated by constructor `new AttributeValue(S = entity.Name)`) will be incredibly faster than using the `System.Text.Json` bridge to handle the Dynamo SDK.