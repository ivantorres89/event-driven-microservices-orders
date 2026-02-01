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
        (N'SUBS-0001', N'Contoso Factura Fácil — Starter (Mensual)', N'Billing', N'Monthly', 9.99),
        (N'SUBS-0002', N'Contoso Factura Fácil — Pro (Mensual)', N'Billing', N'Monthly', 19.99),
        (N'SUBS-0003', N'Contoso Factura Fácil — Business (Mensual)', N'Billing', N'Monthly', 39.99),
        (N'SUBS-0004', N'Contoso Factura Fácil — Starter (Anual)', N'Billing', N'Annual', 99.99),
        (N'SUBS-0005', N'Contoso Factura Fácil — Pro (Anual)', N'Billing', N'Annual', 199.99),
        (N'SUBS-0006', N'Contoso Recordatorios de Cobro — Mensual', N'Billing', N'Monthly', 5.99),

        (N'SUBS-0007', N'Contoso Captura de Tickets — Mensual', N'Expenses', N'Monthly', 6.99),
        (N'SUBS-0008', N'Contoso Captura de Tickets — Anual', N'Expenses', N'Annual', 69.99),
        (N'SUBS-0009', N'Contoso Gastos por Categorías — Mensual', N'Expenses', N'Monthly', 7.99),
        (N'SUBS-0010', N'Contoso Gastos por Categorías — Anual', N'Expenses', N'Annual', 79.99),

        (N'SUBS-0011', N'Contoso Calendario Fiscal — Mensual', N'Tax', N'Monthly', 4.99),
        (N'SUBS-0012', N'Contoso Calendario Fiscal — Anual', N'Tax', N'Annual', 49.99),
        (N'SUBS-0013', N'Contoso IVA Trimestral Asistido — Mensual', N'Tax', N'Monthly', 14.99),
        (N'SUBS-0014', N'Contoso IRPF Trimestral Asistido — Mensual', N'Tax', N'Monthly', 14.99),

        (N'SUBS-0015', N'Contoso Panel de Ventas — Mensual', N'Reporting', N'Monthly', 11.99),
        (N'SUBS-0016', N'Contoso Panel de Ventas — Anual', N'Reporting', N'Annual', 119.99),
        (N'SUBS-0017', N'Contoso Panel de Tesorería — Mensual', N'Reporting', N'Monthly', 12.99),
        (N'SUBS-0018', N'Contoso Panel de Tesorería — Anual', N'Reporting', N'Annual', 129.99),

        (N'SUBS-0019', N'Contoso Multiusuario (hasta 3) — Mensual', N'Collaboration', N'Monthly', 9.99),
        (N'SUBS-0020', N'Contoso Soporte Prioritario — Mensual', N'Collaboration', N'Monthly', 8.99);

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
                    WHEN 0 THEN N'Conciliación Bancaria'
                    WHEN 1 THEN N'Libro Mayor'
                    WHEN 2 THEN N'Gestión de Activos'
                    WHEN 3 THEN N'Anticipos y Provisiones'
                    WHEN 4 THEN N'Alertas de Riesgo'
                    WHEN 5 THEN N'Plantillas de Factura'
                    WHEN 6 THEN N'Exportación CSV/Excel'
                    WHEN 7 THEN N'Reglas de Categoría'
                    WHEN 8 THEN N'Checklist Fiscal'
                    ELSE N'Panel Operativo'
                END + N' — ' + CASE WHEN @period = N'Monthly' THEN N'Mensual' ELSE N'Anual' END;

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
        (N'CUST-0001', N'Ana',    N'García',   N'ana.garcia@contoso.demo',    N'+34-600-000-001', N'11111111A', CAST('1990-01-10' AS DATE), N'Calle Mayor 1',   N'Madrid',    N'28001', N'ES'),
        (N'CUST-0002', N'Luis',   N'Pérez',    N'luis.perez@contoso.demo',   N'+34-600-000-002', N'22222222B', CAST('1987-04-22' AS DATE), N'Gran Vía 10',     N'Madrid',    N'28013', N'ES'),
        (N'CUST-0003', N'Marta',  N'López',    N'marta.lopez@contoso.demo',  N'+34-600-000-003', N'33333333C', CAST('1992-11-05' AS DATE), N'Av. Diagonal 50', N'Barcelona', N'08019', N'ES'),
        (N'CUST-0004', N'Jorge',  N'Ruiz',     N'jorge.ruiz@contoso.demo',   N'+34-600-000-004', N'44444444D', CAST('1985-06-15' AS DATE), N'C/ Serrano 20',   N'Madrid',    N'28006', N'ES'),
        (N'CUST-0005', N'Sofía',  N'Romero',   N'sofia.romero@contoso.demo', N'+34-600-000-005', N'55555555E', CAST('1995-09-30' AS DATE), N'C/ Toledo 7',     N'Madrid',    N'28005', N'ES'),
        (N'CUST-0006', N'Diego',  N'Martín',   N'diego.martin@contoso.demo', N'+34-600-000-006', N'66666666F', CAST('1989-02-12' AS DATE), N'Ronda 3',         N'Valencia',  N'46001', N'ES'),
        (N'CUST-0007', N'Paula',  N'Sánchez',  N'paula.sanchez@contoso.demo',N'+34-600-000-007', N'77777777G', CAST('1991-07-08' AS DATE), N'C/ Marina 12',    N'Barcelona', N'08005', N'ES'),
        (N'CUST-0008', N'Carlos', N'Navarro',  N'carlos.navarro@contoso.demo',N'+34-600-000-008',N'88888888H', CAST('1983-12-25' AS DATE), N'Plaza 4',         N'Sevilla',   N'41001', N'ES'),
        (N'CUST-0009', N'Elena',  N'Torres',   N'elena.torres@contoso.demo', N'+34-600-000-009', N'99999999J', CAST('1993-03-19' AS DATE), N'C/ Sol 9',        N'Málaga',    N'29001', N'ES'),
        (N'CUST-0010', N'Miguel', N'Ortega',   N'miguel.ortega@contoso.demo',N'+34-600-000-010', N'10101010K', CAST('1988-08-02' AS DATE), N'Av. Norte 6',     N'Bilbao',    N'48001', N'ES')
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
