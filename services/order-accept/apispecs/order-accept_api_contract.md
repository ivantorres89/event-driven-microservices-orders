# Order-Accept API contract (for frontend integration)

This document specifies the **expected HTTP status codes** and **JSON response bodies** for the `order-accept` service, aligned with:

- `Customer.ExternalCustomerId` == **userId** from the JWT (claim `sub` or `nameidentifier`)
- SQL schema + seed data conventions (Products contain `ExternalProductId`, `Name`, `Category`, `Vendor`, `ImageUrl`, `Discount`, `BillingPeriod`, `IsSubscription`, `Price`)
- JSON serialization is **camelCase**

> Goal: Provide a stable contract for the frontend and for backend implementation updates.

---

## Conventions

### Pagination
Endpoints that accept `offset` and `size` return a standard page envelope:

```json
{
  "offset": 0,
  "size": 12,
  "total": 145,
  "items": [ /* ... */ ]
}
```

Recommended input constraints:
- `offset >= 0` (default: `0`)
- `size` in `1..100` (default: `12`)

### Error format (ProblemDetails)
Errors should use the standard ASP.NET Core Problem Details format:

- `Content-Type: application/problem+json`

Example:
```json
{
  "type": "https://httpstatuses.com/400",
  "title": "Bad Request",
  "status": 400,
  "detail": "Invalid pagination parameters.",
  "instance": "/api/products?offset=-1&size=0",
  "traceId": "00-...-..."
}
```

Validation errors should use `ValidationProblemDetails`:

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "items[0].quantity": ["Quantity must be greater than 0."]
  },
  "traceId": "00-...-..."
}
```

### Authentication
- **All endpoints** require `Authorization: Bearer <JWT>` (JWT must be valid).
- `/api/products` does not use user identity, but still requires a valid token.
- `/api/orders` extracts the **userId** from the JWT (`sub` or `nameidentifier`) and maps it to `Customer.ExternalCustomerId` for filtering/ownership.

For unauthorized requests:
- `401 Unauthorized` with ProblemDetails.

For forbidden access:
- `403 Forbidden` (token valid but missing required identity claim; or trying to delete an order belonging to a different user)

---

## Data contracts (DTOs)

### ProductDto (summary/detail)
Only fields that add real value to the frontend are included:

```json
{
  "externalProductId": "AZ-700",
  "name": "CertKit AZ-700: Designing and Implementing Azure Networking Solutions",
  "category": "Cloud",
  "vendor": "Microsoft",
  "imageUrl": "https://cdn.jsdelivr.net/gh/.../az-700.png",
  "discount": 35,
  "billingPeriod": "Monthly",
  "isSubscription": true,
  "price": 191.10
}
```

### OrderDto
```json
{
  "id": 123,
  "correlationId": "0a5dfd1a-69d2-4db5-95c0-9d2f18f2a5b3",
  "createdAt": "2026-02-04T10:12:45.123Z",
  "items": [
    {
      "productId": "AZ-700",
      "productName": "CertKit AZ-700: Designing and Implementing Azure Networking Solutions",
      "imageUrl": "https://cdn.jsdelivr.net/gh/.../az-700.png",
      "unitPrice": 191.10,
      "quantity": 1
    }
  ]
}
```

---

## Endpoint specs

### 1) GET /api/products?offset=&size=
Returns a paged list of active, non-soft-deleted products.

**200 OK**
```json
{
  "offset": 0,
  "size": 12,
  "total": 145,
  "items": [ { /* ProductDto */ } ]
}
```

**Possible status codes**
- `200 OK`
- `400 Bad Request` (invalid offset/size)
- `500 Internal Server Error`

---

### 2) GET /api/products/{id}
Where `{id}` is the product **ExternalProductId** (string).

**200 OK**
Returns `ProductDto`.

**Possible status codes**
- `200 OK`
- `400 Bad Request` (missing/invalid id)
- `404 Not Found` (not found, inactive, or soft-deleted)
- `500 Internal Server Error`

---

### 3) GET /api/orders?offset=&size=
Returns a paged list of orders for the authenticated user (`Customer.ExternalCustomerId` from JWT).

**200 OK**
```json
{
  "offset": 0,
  "size": 10,
  "total": 2,
  "items": [ { /* OrderDto */ } ]
}
```

**Possible status codes**
- `200 OK`
- `400 Bad Request` (invalid offset/size)
- `401 Unauthorized` (missing/invalid/expired token)
- `403 Forbidden` (token valid but missing required identity claim, if enforced)
- `500 Internal Server Error`

---

### 4) POST /api/orders
Creates a new order for the authenticated user, and returns order id + correlationId.

**Request body**
```json
{
  "items": [
    { "productId": "AZ-700", "quantity": 1 }
  ]
}
```

Notes:
- `productId` maps to `Product.ExternalProductId`
- Backend generates a `correlationId` (UUID/GUID) for async workflow tracking
- Backend persists `Order` + `OrderItem` rows

**201 Created**
- Should return the full created `OrderDto` (recommended) so UI can show lines immediately.
- Should include `Location: /api/orders/{id}` header (recommended).

```json
{
  "id": 123,
  "correlationId": "0a5dfd1a-69d2-4db5-95c0-9d2f18f2a5b3",
  "createdAt": "2026-02-04T10:12:45.123Z",
  "items": [
    {
      "productId": "AZ-700",
      "productName": "CertKit AZ-700: Designing and Implementing Azure Networking Solutions",
      "imageUrl": "https://cdn.jsdelivr.net/gh/.../az-700.png",
      "unitPrice": 191.10,
      "quantity": 1
    }
  ]
}
```

**Possible status codes**
- `201 Created`
- `400 Bad Request` (items missing/empty; quantity <= 0)
- `401 Unauthorized`
- `404 Not Found` (one or more productId not found or not active)
- `409 Conflict` (optional, only if idempotency or conflict rules exist)
- `500 Internal Server Error`

---

### 5) DELETE /api/orders/{id}
Soft-deletes an order. Backend must ensure the order belongs to the authenticated user.

**204 No Content** (recommended)
No response body.

**Possible status codes**
- `204 No Content`
- `400 Bad Request` (invalid id)
- `401 Unauthorized`
- `403 Forbidden` (order belongs to different user) — OR `404 Not Found` to avoid leaking existence
- `404 Not Found` (does not exist / already soft-deleted)
- `500 Internal Server Error`

---

## Implementation notes (backend)

### User identity mapping
- JWT claim → `userId`
- `userId` must match `Customer.ExternalCustomerId`
- If no matching customer exists, choose one consistent behavior:
  - create customer on first use (recommended for demo), or
  - return `403` or `401`

### Filtering rules
- Products: return only `IsActive = 1` and `IsSoftDeleted = 0`
- Orders: return only `IsSoftDeleted = 0` and owned by authenticated user
- OrderItems: return only `IsSoftDeleted = 0`

---

## Minimal field list (intentionally excludes noise)
Excluded columns because they are not needed by the SPA:
- Product: internal `Id`, `CreatedAt`, `UpdatedAt`, `IsSoftDeleted`, `IsActive` (if endpoint already filters)
- Order: internal `CustomerId`, `IsSoftDeleted` (if endpoint filters)
- Customer: personal data (not used by SPA)
