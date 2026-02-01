/*
  0002_SeedDemoData.sql
  Demo seed data for Contoso:

  - 100 products (including 20 demo-friendly accounting subscriptions)
  - 10 customers
  - 2 orders per customer
  - Randomized number of order items per order (1-5) with randomized quantities (1-3)

  NOTE: avoid GO statements.
*/

SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    ---------------------------
    -- 1) PRODUCTS (100 total)
    ---------------------------

    -- Insert the 20 named subscriptions first (idempotent by ExternalProductId)
    DECLARE @Products TABLE (
        ExternalProductId NVARCHAR(64),
        Name NVARCHAR(200),
        Category NVARCHAR(100),
        BillingPeriod NVARCHAR(16),
        Price DECIMAL(10,2)
    );

    INSERT INTO @Products (ExternalProductId, Name, Category, BillingPeriod, Price)
    VALUES
        (N'SUBS-0001', N'Contoso Easy Invoice — Starter (Monthly)', N'Billing', N'Monthly', 9.99),
        (N'SUBS-0002', N'Contoso Easy Invoice — Pro (Monthly)', N'Billing', N'Monthly', 19.99),
        (N'SUBS-0003', N'Contoso Easy Invoice — Business (Monthly)', N'Billing', N'Monthly', 39.99),
        (N'SUBS-0004', N'Contoso Easy Invoice — Starter (Annual)', N'Billing', N'Annual', 99.99),
        (N'SUBS-0005', N'Contoso Easy Invoice — Pro (Annual)', N'Billing', N'Annual', 199.99),
        (N'SUBS-0006', N'Contoso Payment Reminders — Monthly', N'Billing', N'Monthly', 5.99),

        (N'SUBS-0007', N'Contoso Receipt Capture — Monthly', N'Expenses', N'Monthly', 6.99),
        (N'SUBS-0008', N'Contoso Receipt Capture — Annual', N'Expenses', N'Annual', 69.99),
        (N'SUBS-0009', N'Contoso Categorized Expenses — Monthly', N'Expenses', N'Monthly', 7.99),
        (N'SUBS-0010', N'Contoso Categorized Expenses — Annual', N'Expenses', N'Annual', 79.99),

        (N'SUBS-0011', N'Contoso Tax Calendar — Monthly', N'Tax', N'Monthly', 4.99),
        (N'SUBS-0012', N'Contoso Tax Calendar — Annual', N'Tax', N'Annual', 49.99),
        (N'SUBS-0013', N'Contoso Quarterly VAT Assistant — Monthly', N'Tax', N'Monthly', 14.99),
        (N'SUBS-0014', N'Contoso Quarterly Income Tax Assistant — Monthly', N'Tax', N'Monthly', 14.99),

        (N'SUBS-0015', N'Contoso Sales Dashboard — Monthly', N'Reporting', N'Monthly', 11.99),
        (N'SUBS-0016', N'Contoso Sales Dashboard — Annual', N'Reporting', N'Annual', 119.99),
        (N'SUBS-0017', N'Contoso Cash Flow Dashboard — Monthly', N'Reporting', N'Monthly', 12.99),
        (N'SUBS-0018', N'Contoso Cash Flow Dashboard — Annual', N'Reporting', N'Annual', 129.99),

        (N'SUBS-0019', N'Contoso Multi-user (up to 3) — Monthly', N'Collaboration', N'Monthly', 9.99),
        (N'SUBS-0020', N'Contoso Priority Support — Monthly', N'Collaboration', N'Monthly', 8.99);

    INSERT INTO dbo.Product (ExternalProductId, Name, Category, BillingPeriod, IsSubscription, Price, IsActive)
    SELECT p.ExternalProductId, p.Name, p.Category, p.BillingPeriod, 1, p.Price, 1
    FROM @Products p
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.Product x WHERE x.ExternalProductId = p.ExternalProductId
    );

    -- Create the remaining products up to 100
    DECLARE @i INT = 21;
    WHILE @i <= 100
    BEGIN
        DECLARE @ext NVARCHAR(64) = N'CONTOSO-' + RIGHT(N'0000' + CAST(@i AS NVARCHAR(10)), 4);

        IF NOT EXISTS (SELECT 1 FROM dbo.Product WHERE ExternalProductId = @ext)
        BEGIN
            DECLARE @category NVARCHAR(100) = CASE (@i % 6)
                WHEN 0 THEN N'Billing'
                WHEN 1 THEN N'Expenses'
                WHEN 2 THEN N'Tax'
                WHEN 3 THEN N'Reporting'
                WHEN 4 THEN N'Collaboration'
                ELSE N'Compliance'
            END;

            DECLARE @period NVARCHAR(16) = CASE WHEN (@i % 2) = 0 THEN N'Monthly' ELSE N'Annual' END;

            DECLARE @name NVARCHAR(200) = N'Contoso ' +
                CASE (@i % 10)
                    WHEN 0 THEN N'Bank Reconciliation'
                    WHEN 1 THEN N'General Ledger'
                    WHEN 2 THEN N'Asset Management'
                    WHEN 3 THEN N'Prepayments & Provisions'
                    WHEN 4 THEN N'Risk Alerts'
                    WHEN 5 THEN N'Invoice Templates'
                    WHEN 6 THEN N'CSV/Excel Export'
                    WHEN 7 THEN N'Categorization Rules'
                    WHEN 8 THEN N'Tax Checklist'
                    ELSE N'Operations Dashboard'
                END + N' — ' + CASE WHEN @period = N'Monthly' THEN N'Monthly' ELSE N'Annual' END;

            DECLARE @price DECIMAL(10,2) = CAST((5 + (@i % 25)) + CASE WHEN @period = N'Annual' THEN 60 ELSE 0 END AS DECIMAL(10,2));

            INSERT INTO dbo.Product (ExternalProductId, Name, Category, BillingPeriod, IsSubscription, Price, IsActive)
            VALUES (@ext, @name, @category, @period, 1, @price, 1);
        END

        SET @i += 1;
    END

    ---------------------------
    -- 2) CUSTOMERS (10 total)
    ---------------------------

    DECLARE @Customers TABLE (Id BIGINT, ExternalCustomerId NVARCHAR(64));

    INSERT INTO dbo.Customer (
        ExternalCustomerId,
        FirstName,
        LastName,
        Email,
        PhoneNumber,
        NationalId,
        DateOfBirth,
        AddressLine1,
        City,
        PostalCode,
        CountryCode
    )
    OUTPUT inserted.Id, inserted.ExternalCustomerId INTO @Customers (Id, ExternalCustomerId)
    SELECT v.ExternalCustomerId, v.FirstName, v.LastName, v.Email, v.Phone, v.NationalId, v.DateOfBirth,
           v.AddressLine1, v.City, v.PostalCode, v.CountryCode
        FROM (VALUES
            (N'CUST-0001', N'Alice',   N'Johnson',   N'alice.johnson@contoso.demo',   N'+1-206-555-0001', N'NID-0001', CAST('1990-01-10' AS DATE), N'100 Pike St', N'Seattle', N'98101', N'US'),
            (N'CUST-0002', N'Bob',   N'Smith',   N'bob.smith@contoso.demo',   N'+1-415-555-0002', N'NID-0002', CAST('1987-04-22' AS DATE), N'200 Market St', N'San Francisco', N'94105', N'US'),
            (N'CUST-0003', N'Carol',   N'Davis',   N'carol.davis@contoso.demo',   N'+1-212-555-0003', N'NID-0003', CAST('1992-11-05' AS DATE), N'350 Madison Ave', N'New York', N'10017', N'US'),
            (N'CUST-0004', N'David',   N'Wilson',   N'david.wilson@contoso.demo',   N'+44-20-7946-0004', N'NID-0004', CAST('1985-06-15' AS DATE), N'10 Downing St', N'London', N'SW1A 2AA', N'GB'),
            (N'CUST-0005', N'Emma',   N'Brown',   N'emma.brown@contoso.demo',   N'+44-161-555-0005', N'NID-0005', CAST('1995-09-30' AS DATE), N'1 Deansgate', N'Manchester', N'M3 1AZ', N'GB'),
            (N'CUST-0006', N'Frank',   N'Miller',   N'frank.miller@contoso.demo',   N'+1-416-555-0006', N'NID-0006', CAST('1989-02-12' AS DATE), N'20 King St W', N'Toronto', N'M5H 1C4', N'CA'),
            (N'CUST-0007', N'Grace',   N'Taylor',   N'grace.taylor@contoso.demo',   N'+61-2-5550-0007', N'NID-0007', CAST('1991-07-08' AS DATE), N'5 Martin Pl', N'Sydney', N'2000', N'AU'),
            (N'CUST-0008', N'Henry',   N'Anderson',   N'henry.anderson@contoso.demo',   N'+49-30-5550-0008', N'NID-0008', CAST('1983-12-25' AS DATE), N'Unter den Linden 77', N'Berlin', N'10117', N'DE'),
            (N'CUST-0009', N'Irene',   N'Thomas',   N'irene.thomas@contoso.demo',   N'+33-1-5550-0009', N'NID-0009', CAST('1993-03-19' AS DATE), N'12 Rue de Rivoli', N'Paris', N'75001', N'FR'),
            (N'CUST-0010', N'Jack',   N'Moore',   N'jack.moore@contoso.demo',   N'+39-06-5550-0010', N'NID-0010', CAST('1988-08-02' AS DATE), N'Via del Corso 10', N'Rome', N'00186', N'IT')
        ) AS v(ExternalCustomerId, FirstName, LastName, Email, Phone, NationalId, DateOfBirth, AddressLine1, City, PostalCode, CountryCode)
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.Customer c WHERE c.ExternalCustomerId = v.ExternalCustomerId
    );

    -- If customers were already present, load their Ids too (for idempotent re-runs)
    INSERT INTO @Customers (Id, ExternalCustomerId)
    SELECT c.Id, c.ExternalCustomerId
    FROM dbo.Customer c
    WHERE c.ExternalCustomerId LIKE N'CUST-%'
      AND NOT EXISTS (SELECT 1 FROM @Customers x WHERE x.Id = c.Id);

    ---------------------------
    -- 3) ORDERS (2 per customer)
    ---------------------------

    DECLARE @Orders TABLE (Id BIGINT, CustomerId BIGINT);

    INSERT INTO dbo.[Order] (CorrelationId, CustomerId)
    OUTPUT inserted.Id, inserted.CustomerId INTO @Orders (Id, CustomerId)
    SELECT CONVERT(NVARCHAR(64), NEWID()), c.Id
    FROM @Customers c
    CROSS JOIN (VALUES(1),(2)) AS n(x)
    WHERE NOT EXISTS (
        -- basic idempotency: ensure each customer doesn't exceed 2 demo orders
        SELECT 1
        FROM dbo.[Order] o
        WHERE o.CustomerId = c.Id
          AND o.CorrelationId LIKE N'%-%-%-%-%'
        HAVING COUNT(*) >= 2
    );

    -- If orders already exist, include up to 20 of them (10 customers * 2)
    IF NOT EXISTS (SELECT 1 FROM @Orders)
    BEGIN
        INSERT INTO @Orders (Id, CustomerId)
        SELECT TOP (20) o.Id, o.CustomerId
        FROM dbo.[Order] o
        ORDER BY o.Id DESC;
    END

    ---------------------------
    -- 4) ORDER ITEMS (randomized)
    ---------------------------

    ;WITH nums AS (
        SELECT 1 AS n UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4 UNION ALL SELECT 5
    )
    INSERT INTO dbo.OrderItem (OrderId, ProductId, Quantity)
    SELECT o.Id,
           p.Id,
           1 + (ABS(CHECKSUM(NEWID())) % 3)
    FROM @Orders o
    CROSS APPLY (
        SELECT TOP (1 + (ABS(CHECKSUM(NEWID())) % 5)) pr.Id
        FROM dbo.Product pr
        ORDER BY NEWID()
    ) p
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.OrderItem oi WHERE oi.OrderId = o.Id
    );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @msg NVARCHAR(4000) = ERROR_MESSAGE();
    RAISERROR(@msg, 16, 1);
END CATCH;
