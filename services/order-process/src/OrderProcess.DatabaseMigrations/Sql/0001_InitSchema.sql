/*
  0001_InitSchema.sql
  Database-first DDL (SQL Server / Azure SQL Database)

  - Creates core tables: Customer, Product, [Order], OrderItem
  - Adds CreatedAt + UpdatedAt on ALL tables
    * CreatedAt: DEFAULT SYSUTCDATETIME()
    * UpdatedAt: DEFAULT SYSUTCDATETIME() on INSERT + trigger on UPDATE

  NOTE: avoid GO statements (runner executes as a single batch).
*/

SET NOCOUNT ON;

-- --- Customer ---
IF OBJECT_ID(N'dbo.Customer', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Customer
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customer PRIMARY KEY,
        ExternalCustomerId NVARCHAR(64) NOT NULL,
        FirstName NVARCHAR(100) NOT NULL,
        LastName NVARCHAR(100) NOT NULL,
        Email NVARCHAR(256) NOT NULL,
        PhoneNumber NVARCHAR(32) NOT NULL,
        NationalId NVARCHAR(32) NOT NULL,
        DateOfBirth DATE NULL,
        AddressLine1 NVARCHAR(200) NOT NULL,
        City NVARCHAR(100) NOT NULL,
        PostalCode NVARCHAR(20) NOT NULL,
        CountryCode NVARCHAR(2) NOT NULL,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_Customer_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_Customer_UpdatedAt DEFAULT SYSUTCDATETIME(),
        IsSoftDeleted BIT NOT NULL CONSTRAINT DF_Customer_IsSoftDeleted DEFAULT (0)
    );

    CREATE UNIQUE INDEX UX_Customer_ExternalCustomerId ON dbo.Customer (ExternalCustomerId);
END

IF COL_LENGTH(N'dbo.Customer', N'IsSoftDeleted') IS NULL
BEGIN
    ALTER TABLE dbo.Customer
        ADD IsSoftDeleted BIT NOT NULL CONSTRAINT DF_Customer_IsSoftDeleted DEFAULT (0);
END

-- --- Product ---
IF OBJECT_ID(N'dbo.Product', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Product
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Product PRIMARY KEY,
        ExternalProductId NVARCHAR(64) NOT NULL,
        Name NVARCHAR(200) NOT NULL,
        Category NVARCHAR(100) NOT NULL,
        BillingPeriod NVARCHAR(16) NOT NULL,
        IsSubscription BIT NOT NULL CONSTRAINT DF_Product_IsSubscription DEFAULT (1),
        Price DECIMAL(10,2) NOT NULL CONSTRAINT DF_Product_Price DEFAULT (0),
        IsActive BIT NOT NULL CONSTRAINT DF_Product_IsActive DEFAULT (1),
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_Product_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_Product_UpdatedAt DEFAULT SYSUTCDATETIME(),
        IsSoftDeleted BIT NOT NULL CONSTRAINT DF_Product_IsSoftDeleted DEFAULT (0),
        CONSTRAINT CK_Product_BillingPeriod CHECK (BillingPeriod IN (N'Monthly', N'Annual')),
        CONSTRAINT CK_Product_PriceNonNegative CHECK (Price >= (0))
    );

    CREATE UNIQUE INDEX UX_Product_ExternalProductId ON dbo.Product (ExternalProductId);
END

IF COL_LENGTH(N'dbo.Product', N'IsSoftDeleted') IS NULL
BEGIN
    ALTER TABLE dbo.Product
        ADD IsSoftDeleted BIT NOT NULL CONSTRAINT DF_Product_IsSoftDeleted DEFAULT (0);
END

-- --- Order ---
IF OBJECT_ID(N'dbo.[Order]', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[Order]
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Order PRIMARY KEY,
        CorrelationId NVARCHAR(64) NOT NULL,
        CustomerId BIGINT NOT NULL,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_Order_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_Order_UpdatedAt DEFAULT SYSUTCDATETIME(),
        IsSoftDeleted BIT NOT NULL CONSTRAINT DF_Order_IsSoftDeleted DEFAULT (0),
        CONSTRAINT FK_Order_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.Customer (Id) ON DELETE NO ACTION
    );

    CREATE UNIQUE INDEX UX_Order_CorrelationId ON dbo.[Order] (CorrelationId);
    CREATE INDEX IX_Order_CustomerId ON dbo.[Order] (CustomerId);
END

IF COL_LENGTH(N'dbo.[Order]', N'IsSoftDeleted') IS NULL
BEGIN
    ALTER TABLE dbo.[Order]
        ADD IsSoftDeleted BIT NOT NULL CONSTRAINT DF_Order_IsSoftDeleted DEFAULT (0);
END

-- --- OrderItem ---
IF OBJECT_ID(N'dbo.OrderItem', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderItem
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderItem PRIMARY KEY,
        OrderId BIGINT NOT NULL,
        ProductId BIGINT NOT NULL,
        Quantity INT NOT NULL,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_OrderItem_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_OrderItem_UpdatedAt DEFAULT SYSUTCDATETIME(),
        IsSoftDeleted BIT NOT NULL CONSTRAINT DF_OrderItem_IsSoftDeleted DEFAULT (0),
        CONSTRAINT CK_OrderItem_QuantityPositive CHECK (Quantity > 0),
        CONSTRAINT FK_OrderItem_Order FOREIGN KEY (OrderId) REFERENCES dbo.[Order] (Id) ON DELETE CASCADE,
        CONSTRAINT FK_OrderItem_Product FOREIGN KEY (ProductId) REFERENCES dbo.Product (Id) ON DELETE NO ACTION
    );

    CREATE INDEX IX_OrderItem_OrderId ON dbo.OrderItem (OrderId);
    CREATE INDEX IX_OrderItem_ProductId ON dbo.OrderItem (ProductId);
END

IF COL_LENGTH(N'dbo.OrderItem', N'IsSoftDeleted') IS NULL
BEGIN
    ALTER TABLE dbo.OrderItem
        ADD IsSoftDeleted BIT NOT NULL CONSTRAINT DF_OrderItem_IsSoftDeleted DEFAULT (0);
END

-- --- UpdatedAt triggers ---
-- Customer
IF OBJECT_ID(N'dbo.trg_Customer_SetUpdatedAt', N'TR') IS NOT NULL
    DROP TRIGGER dbo.trg_Customer_SetUpdatedAt;
EXEC(N'
CREATE TRIGGER dbo.trg_Customer_SetUpdatedAt
ON dbo.Customer
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE c
    SET UpdatedAt = SYSUTCDATETIME()
    FROM dbo.Customer c
    INNER JOIN inserted i ON c.Id = i.Id;
END
');

-- Product
IF OBJECT_ID(N'dbo.trg_Product_SetUpdatedAt', N'TR') IS NOT NULL
    DROP TRIGGER dbo.trg_Product_SetUpdatedAt;
EXEC(N'
CREATE TRIGGER dbo.trg_Product_SetUpdatedAt
ON dbo.Product
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE p
    SET UpdatedAt = SYSUTCDATETIME()
    FROM dbo.Product p
    INNER JOIN inserted i ON p.Id = i.Id;
END
');

-- Order
IF OBJECT_ID(N'dbo.trg_Order_SetUpdatedAt', N'TR') IS NOT NULL
    DROP TRIGGER dbo.trg_Order_SetUpdatedAt;
EXEC(N'
CREATE TRIGGER dbo.trg_Order_SetUpdatedAt
ON dbo.[Order]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE o
    SET UpdatedAt = SYSUTCDATETIME()
    FROM dbo.[Order] o
    INNER JOIN inserted i ON o.Id = i.Id;
END
');

-- OrderItem
IF OBJECT_ID(N'dbo.trg_OrderItem_SetUpdatedAt', N'TR') IS NOT NULL
    DROP TRIGGER dbo.trg_OrderItem_SetUpdatedAt;
EXEC(N'
CREATE TRIGGER dbo.trg_OrderItem_SetUpdatedAt
ON dbo.OrderItem
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE oi
    SET UpdatedAt = SYSUTCDATETIME()
    FROM dbo.OrderItem oi
    INNER JOIN inserted i ON oi.Id = i.Id;
END
');
