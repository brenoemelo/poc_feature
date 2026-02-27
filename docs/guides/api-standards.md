# API Design Standards

We follow **Pragmatic REST** principles with strict adherence to **RFC 7807** for errors and **HATEOAS** for navigability.

## 1. Response Envelope

All API responses (success or failure) follow a predictable structure.

### Success Response (Collection)
```json
{
  "data": [
    { "id": "mat-1", "name": "Polycarbonate" },
    { "id": "mat-2", "name": "Aluminum" }
  ],
  "meta": {
    "count": 2,
    "nextCursor": "VGhpcyBpcyBhIGJhc2U2NCB0b2tlbg=="
  },
  "links": [
    { "rel": "self", "href": "/api/v1/materials?limit=10", "method": "GET" },
    { "rel": "next", "href": "/api/v1/materials?limit=10&cursor=VGhpcy...", "method": "GET" }
  ]
}
```

### Success Response (Single Resource)
```json
{
  "data": { "id": "mat-1", "name": "Polycarbonate" },
  "links": [
    { "rel": "self", "href": "/api/v1/materials/mat-1", "method": "GET" },
    { "rel": "delete", "href": "/api/v1/materials/mat-1", "method": "DELETE" }
  ]
}
```

## 2. Internal Control Flow (Result Pattern)

While the API returns standard JSON, the internal application uses the **Result Pattern** (`PoC.Shared.Common.Result<T>`) to handle logic flow without Exceptions.

*   **Success:** `Result.Success(value)` -> Mapped to `200 OK` (with Envelope).
*   **Failure:** `Result.Failure(Error.NotFound)` -> Mapped to `404 Not Found` (ProblemDetails).
*   **Validation:** `Result.Failure(Error.Validation)` -> Mapped to `400 Bad Request`.

## 3. Error Handling (RFC 7807)

We do **NOT** return `200 OK` for errors. We use the standard `ProblemDetails` format.

**Example: Validation Error (400 Bad Request)**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Validation Failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "errors": {
    "Name": ["'Name' must not be empty."],
    "Price": ["'Price' must be greater than 0."]
  },
  "traceId": "00-d4c8e9...-01"
}
```

## 4. Pagination Strategy (Cursor-Based)

Due to DynamoDB's architecture, we avoid "Offset/Limit" (Skip/Take) pagination. We use **Cursor-Based Pagination**.

### How it works
1. **Client** requests the first page: `GET /items?limit=10`
2. **Server** returns items + `meta.nextCursor` (Base64-encoded `LastEvaluatedKey`).
3. **Client** requests the next page: `GET /items?limit=10&cursor=VGhpcy...`

## 5. Versioning

All public APIs are versioned in the URI path and aligned with Terraform service deployment modules.
*   Pattern: `/api/v{major}/{resource}`
*   Example: `/api/v1/materials`
*   Terraform Alignment: API versioning is reflected in `terraform/services/{service}/main.tf` for API Gateway configuration.

All public APIs are versioned in the URI path.
*   Pattern: `/api/v{major}/{resource}`
*   Example: `/api/v1/materials`

## 6. HATEOAS (Hypermedia)

We implement **Level 3** of the Richardson Maturity Model where possible.
*   **Links Array:** Every resource should provide links to related actions.
*   **Discovery:** Clients should ideally rely on `links` rather than hardcoding URL construction logic.
