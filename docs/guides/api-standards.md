# API Design Standards

We follow **Pragmatic REST** principles with strict adherence to **RFC 7807** for errors and **HATEOAS** for navigability.

## 1. Response Envelope context

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

## 2. Error Handling (RFC 7807)

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

**Common Status Codes:**
- `200 OK`: Success (Synchronous).
- `201 Created`: Resource created.
- `202 Accepted`: Request accepted for background processing (Async).
- `400 Bad Request`: Validation or syntax error.
- `404 Not Found`: Resource does not exist.
- `422 Unprocessable Entity`: Business rule violation.
- `500 Internal Server Error`: Unhandled exception (Bug).

## 3. Pagination Strategy (Cursor-Based)

Due to DynamoDB's architecture, we avoid "Offset/Limit" (Skip/Take) pagination as it performs poorly at scale. Instead, we use **Cursor-Based Pagination**.

### How it works
1. **Client** requests the first page: `GET /items?limit=10`
2. **Server** returns items + `meta.nextCursor`.
   - The cursor is the Base64-encoded `LastEvaluatedKey` from DynamoDB.
3. **Client** requests the next page: `GET /items?limit=10&cursor=VGhpcy...`

> **Note:** This implies **Forward-Only** navigation.

## 4. versioning

All public APIs are versioned in the URI path.
- Pattern: `/api/v{major}/{resource}`
- Example: `/api/v1/materials`

Any breaking change requires incrementing the version (e.g., `v2`).

## 5. HATEOAS (Hypermedia)

We implement **Level 3** of the Richardson Maturity Model where possible.
- **Links Array:** Every resource should provide links to related actions.
- **Discovery:** Clients should ideally rely on `links` rather than hardcoding URL construction logic, although pragmatic coupling is accepted for internal microservices.
