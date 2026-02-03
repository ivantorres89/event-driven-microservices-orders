/*
  0002_SeedDemoData.sql
  Demo seed data for Contoso:

  - 145 products (certification-prep subscriptions)
  - 10 customers
  - 2 orders per customer
  - Randomized number of order items per order (1-5) with randomized quantities (1-3)

  NOTE: avoid GO statements.
*/

SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    ---------------------------
    -- 1) PRODUCTS (145 total)
    ---------------------------

    -- Contoso now sells certification-prep subscriptions (idempotent by ExternalProductId).
    -- ImageUrl points to a public static host (recommended: GitHub repo + jsDelivr).
    DECLARE @Products TABLE (
        ExternalProductId NVARCHAR(64),
        Name NVARCHAR(200),
        Category NVARCHAR(100),
        BillingPeriod NVARCHAR(16),
        Price DECIMAL(10,2),
        Vendor NVARCHAR(64),
        ImageUrl NVARCHAR(2048),
        Discount INT
    );

    INSERT INTO @Products (ExternalProductId, Name, Category, BillingPeriod, Price, Vendor, ImageUrl, Discount)
    VALUES
        (N'AZ-900', N'Microsoft Azure Fundamentals', N'Cloud Fundamentals', N'Annual', 199.90, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/AZ-900.png', 35),
        (N'AZ-104', N'Microsoft Azure Administrator', N'Cloud Administration', N'Monthly', 39.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/AZ-104.png', 25),
        (N'AZ-204', N'Developing Solutions for Microsoft Azure', N'Cloud Development', N'Annual', 399.90, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/AZ-204.png', 36),
        (N'AZ-305', N'Designing Microsoft Azure Infrastructure Solutions', N'Cloud Architecture', N'Monthly', 69.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/AZ-305.png', 0),
        (N'AZ-400', N'Designing and Implementing Microsoft DevOps Solutions', N'DevOps', N'Monthly', 69.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/AZ-400.png', 20),
        (N'AZ-500', N'Microsoft Azure Security Technologies', N'Cloud Security', N'Monthly', 39.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/AZ-500.png', 30),
        (N'AZ-700', N'Designing and Implementing Microsoft Azure Networking Solutions', N'Networking', N'Monthly', 39.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/AZ-700.png', 30),
        (N'AZ-800', N'Administering Windows Server Hybrid Core Infrastructure', N'Hybrid Infrastructure', N'Annual', 399.90, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/AZ-800.png', 25),
        (N'AZ-801', N'Configuring Windows Server Hybrid Advanced Services', N'Hybrid Infrastructure', N'Annual', 399.90, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/AZ-801.png', 10),
        (N'AZ-140', N'Configuring and Operating Microsoft Azure Virtual Desktop', N'End-User Computing', N'Monthly', 39.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/AZ-140.png', 20),
        (N'AZ-120', N'Planning and Administering Microsoft Azure for SAP Workloads', N'Enterprise Workloads', N'Annual', 749.90, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/AZ-120.png', 35),
        (N'DP-900', N'Microsoft Azure Data Fundamentals', N'Data Fundamentals', N'Monthly', 19.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/DP-900.png', 25),
        (N'DP-203', N'Data Engineering on Microsoft Azure', N'Data Engineering', N'Annual', 399.90, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/DP-203.png', 60),
        (N'DP-300', N'Administering Microsoft Azure SQL Solutions', N'Database', N'Monthly', 39.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/DP-300.png', 20),
        (N'DP-100', N'Designing and Implementing a Data Science Solution on Azure', N'Machine Learning', N'Annual', 399.90, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/DP-100.png', 30),
        (N'DP-420', N'Designing and Implementing Cloud-Native Applications Using Microsoft Azure Cosmos DB', N'Cloud Databases', N'Monthly', 74.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/DP-420.png', 30),
        (N'AI-900', N'Microsoft Azure AI Fundamentals', N'AI Fundamentals', N'Annual', 199.90, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/AI-900.png', 60),
        (N'AI-102', N'Designing and Implementing a Microsoft Azure AI Solution', N'AI Engineering', N'Monthly', 39.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/AI-102.png', 60),
        (N'SC-900', N'Microsoft Security, Compliance, and Identity Fundamentals', N'Security Fundamentals', N'Monthly', 19.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/SC-900.png', 35),
        (N'SC-100', N'Microsoft Cybersecurity Architect', N'Cybersecurity Architecture', N'Annual', 699.90, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/SC-100.png', 0),
        (N'SC-200', N'Microsoft Security Operations Analyst', N'Security Operations', N'Annual', 399.90, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/SC-200.png', 10),
        (N'SC-300', N'Microsoft Identity and Access Administrator', N'Identity', N'Annual', 399.90, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/SC-300.png', 30),
        (N'SC-400', N'Microsoft Information Protection Administrator', N'Compliance', N'Annual', 399.90, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/SC-400.png', 35),
        (N'SC-401', N'Microsoft Cybersecurity Governance and Risk Management', N'GRC', N'Monthly', 39.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/SC-401.png', 20),
        (N'MS-900', N'Microsoft 365 Fundamentals', N'M365 Fundamentals', N'Annual', 199.90, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/MS-900.png', 30),
        (N'MS-102', N'Microsoft 365 Administrator', N'M365 Administration', N'Monthly', 39.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/MS-102.png', 25),
        (N'MS-700', N'Managing Microsoft Teams', N'Collaboration', N'Monthly', 39.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/MS-700.png', 50),
        (N'MD-102', N'Endpoint Administrator', N'Endpoint Management', N'Monthly', 39.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/MD-102.png', 10),
        (N'PL-900', N'Microsoft Power Platform Fundamentals', N'Power Platform Fundamentals', N'Annual', 199.90, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/PL-900.png', 40),
        (N'PL-100', N'Microsoft Power Platform App Maker', N'Power Apps', N'Annual', 399.90, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/PL-100.png', 15),
        (N'PL-200', N'Microsoft Power Platform Functional Consultant', N'Power Platform', N'Monthly', 39.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/PL-200.png', 30),
        (N'PL-300', N'Microsoft Power BI Data Analyst', N'Business Intelligence', N'Monthly', 39.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/PL-300.png', 20),
        (N'PL-400', N'Microsoft Power Platform Developer', N'Power Platform Development', N'Annual', 399.90, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/PL-400.png', 36),
        (N'MB-910', N'Microsoft Dynamics 365 Fundamentals (CRM)', N'Dynamics Fundamentals', N'Monthly', 19.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/MB-910.png', 35),
        (N'MB-920', N'Microsoft Dynamics 365 Fundamentals (ERP)', N'Dynamics Fundamentals', N'Monthly', 19.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/MB-920.png', 36),
        (N'MB-210', N'Microsoft Dynamics 365 Sales Functional Consultant', N'Dynamics Sales', N'Monthly', 39.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/MB-210.png', 30),
        (N'MB-230', N'Microsoft Dynamics 365 Customer Service Functional Consultant', N'Dynamics Service', N'Monthly', 39.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/MB-230.png', 20),
        (N'MB-260', N'Microsoft Dynamics 365 Customer Insights (Data) Specialist', N'Customer Data Platforms', N'Monthly', 39.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/MB-260.png', 40),
        (N'MB-330', N'Microsoft Dynamics 365 Supply Chain Management Functional Consultant', N'Dynamics SCM', N'Annual', 399.90, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/MB-330.png', 40),
        (N'MB-700', N'Microsoft Dynamics 365 Finance and Operations Apps Solution Architect', N'Dynamics Architecture', N'Annual', 699.90, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/MB-700.png', 10),
        (N'GH-900', N'GitHub Foundations', N'DevOps', N'Annual', 199.90, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/GH-900.png', 25),
        (N'GH-100', N'GitHub Administration', N'DevOps', N'Annual', 399.90, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/GH-100.png', 0),
        (N'GH-200', N'GitHub Actions', N'DevOps', N'Annual', 399.90, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/GH-200.png', 0),
        (N'GH-300', N'GitHub Copilot', N'Developer Productivity', N'Monthly', 39.99, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/GH-300.png', 10),
        (N'GH-500', N'GitHub Advanced Security', N'Application Security', N'Annual', 399.90, N'Microsoft', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/GH-500.png', 25),
        (N'CLF-C02', N'AWS Certified Cloud Practitioner', N'Cloud Fundamentals', N'Annual', 199.90, N'AWS', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/CLF-C02.png', 36),
        (N'AIF-C01', N'AWS Certified AI Practitioner', N'AI Fundamentals', N'Annual', 199.90, N'AWS', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/AIF-C01.png', 36),
        (N'SAA-C03', N'AWS Certified Solutions Architect - Associate', N'Cloud Architecture', N'Annual', 399.90, N'AWS', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/SAA-C03.png', 30),
        (N'DVA-C02', N'AWS Certified Developer - Associate', N'Cloud Development', N'Annual', 399.90, N'AWS', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/DVA-C02.png', 40),
        (N'SOA-C03', N'AWS Certified CloudOps Engineer - Associate', N'Cloud Operations', N'Annual', 399.90, N'AWS', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/SOA-C03.png', 20),
        (N'DEA-C01', N'AWS Certified Data Engineer - Associate', N'Data Engineering', N'Monthly', 39.99, N'AWS', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/DEA-C01.png', 36),
        (N'MLA-C01', N'AWS Certified Machine Learning Engineer - Associate', N'Machine Learning', N'Annual', 399.90, N'AWS', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/MLA-C01.png', 0),
        (N'SAP-C02', N'AWS Certified Solutions Architect - Professional', N'Cloud Architecture', N'Annual', 599.90, N'AWS', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/SAP-C02.png', 50),
        (N'DOP-C02', N'AWS Certified DevOps Engineer - Professional', N'DevOps', N'Annual', 599.90, N'AWS', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/DOP-C02.png', 25),
        (N'ANS-C01', N'AWS Certified Advanced Networking - Specialty', N'Networking', N'Annual', 749.90, N'AWS', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ANS-C01.png', 36),
        (N'SCS-C01', N'AWS Certified Security - Specialty', N'Cloud Security', N'Monthly', 74.99, N'AWS', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/SCS-C01.png', 20),
        (N'MLS-C01', N'AWS Certified Machine Learning - Specialty', N'Machine Learning', N'Monthly', 74.99, N'AWS', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/MLS-C01.png', 35),
        (N'200-301', N'Cisco Certified Network Associate (CCNA)', N'Networking', N'Monthly', 39.99, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/200-301.png', 10),
        (N'350-401', N'Implementing Cisco Enterprise Network Core Technologies (ENCOR)', N'Networking', N'Monthly', 59.99, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/350-401.png', 15),
        (N'300-410', N'Implementing Cisco Enterprise Advanced Routing and Services (ENARSI)', N'Networking', N'Monthly', 79.99, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/300-410.png', 30),
        (N'300-415', N'Implementing Cisco SD-WAN Solutions (ENSDWI)', N'Networking', N'Annual', 799.90, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/300-415.png', 25),
        (N'300-420', N'Designing Cisco Enterprise Networks (ENSLD)', N'Networking', N'Annual', 799.90, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/300-420.png', 20),
        (N'300-425', N'Designing Cisco Wireless Networks (ENWLSD)', N'Networking', N'Annual', 799.90, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/300-425.png', 20),
        (N'300-430', N'Implementing Cisco Enterprise Wireless Networks (ENWLSI)', N'Networking', N'Monthly', 79.99, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/300-430.png', 30),
        (N'300-435', N'Automating Cisco Enterprise Solutions (ENAUTO)', N'DevOps', N'Monthly', 79.99, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/300-435.png', 40),
        (N'300-440', N'Designing and Implementing Cloud Connectivity (ENCC)', N'Cloud Networking', N'Annual', 799.90, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/300-440.png', 15),
        (N'300-445', N'Designing and Implementing Enterprise Network Assurance (ENNA)', N'Observability', N'Monthly', 79.99, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/300-445.png', 40),
        (N'300-710', N'Implementing and Configuring Cisco Identity Services Engine (SISE)', N'Identity', N'Monthly', 79.99, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/300-710.png', 20),
        (N'300-715', N'Implementing and Configuring Cisco Secure Email Gateway (SENSA)', N'Cybersecurity', N'Annual', 799.90, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/300-715.png', 25),
        (N'300-720', N'Securing Email with Cisco Email Security Appliance (SESA)', N'Cybersecurity', N'Annual', 799.90, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/300-720.png', 40),
        (N'300-725', N'Securing the Web with Cisco Secure Web Appliance (SWSA)', N'Cybersecurity', N'Monthly', 79.99, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/300-725.png', 35),
        (N'350-701', N'Implementing and Operating Cisco Security Core Technologies (SCOR)', N'Cybersecurity', N'Monthly', 59.99, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/350-701.png', 30),
        (N'200-901', N'Developing Applications Using Cisco Core Platforms and APIs (DEVASC)', N'Software Development', N'Annual', 399.90, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/200-901.png', 20),
        (N'350-901', N'Developing Applications Using Cisco Core Platforms and APIs (DEVCOR)', N'Software Development', N'Monthly', 59.99, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/350-901.png', 15),
        (N'300-910', N'Developing Applications Using Cisco Core Platforms and APIs (DEVWBX)', N'Collaboration', N'Monthly', 79.99, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/300-910.png', 25),
        (N'350-801', N'Implementing and Operating Cisco Collaboration Core Technologies (CLCOR)', N'Collaboration', N'Monthly', 59.99, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/350-801.png', 30),
        (N'300-810', N'Implementing Cisco Collaboration Applications (CLICA)', N'Collaboration', N'Monthly', 79.99, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/300-810.png', 25),
        (N'300-815', N'Implementing Cisco Advanced Call Control and Mobility Services (CLACCM)', N'Collaboration', N'Monthly', 79.99, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/300-815.png', 25),
        (N'300-820', N'Implementing Cisco Collaboration Cloud and Edge Solutions (CLCEI)', N'Collaboration', N'Monthly', 79.99, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/300-820.png', 50),
        (N'300-835', N'Automating Cisco Collaboration Solutions (CLAUTO)', N'DevOps', N'Annual', 799.90, N'Cisco', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/300-835.png', 40),
        (N'220-1201', N'CompTIA A+ Core 1', N'IT Support', N'Monthly', 19.99, N'CompTIA', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/220-1201.png', 20),
        (N'220-1202', N'CompTIA A+ Core 2', N'IT Support', N'Monthly', 19.99, N'CompTIA', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/220-1202.png', 10),
        (N'FC0-U71', N'CompTIA Tech+', N'IT Fundamentals', N'Annual', 199.90, N'CompTIA', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/FC0-U71.png', 40),
        (N'N10-009', N'CompTIA Network+', N'Networking', N'Annual', 399.90, N'CompTIA', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/N10-009.png', 36),
        (N'SY0-701', N'CompTIA Security+', N'Cybersecurity', N'Monthly', 39.99, N'CompTIA', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/SY0-701.png', 50),
        (N'PT0-003', N'CompTIA PenTest+', N'Offensive Security', N'Monthly', 59.99, N'CompTIA', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/PT0-003.png', 10),
        (N'CY0-002', N'CompTIA CySA+', N'Security Operations', N'Monthly', 59.99, N'CompTIA', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/CY0-002.png', 50),
        (N'CAS-005', N'CompTIA SecurityX (CASP+)', N'Advanced Security', N'Monthly', 69.99, N'CompTIA', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/CAS-005.png', 10),
        (N'XK0-005', N'CompTIA Linux+', N'Linux', N'Annual', 399.90, N'CompTIA', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/XK0-005.png', 35),
        (N'SK0-005', N'CompTIA Server+', N'Servers', N'Annual', 399.90, N'CompTIA', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/SK0-005.png', 15),
        (N'PK0-005', N'CompTIA Project+', N'Project Management', N'Annual', 399.90, N'CompTIA', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/PK0-005.png', 36),
        (N'CV0-004', N'CompTIA Cloud+', N'Cloud', N'Annual', 399.90, N'CompTIA', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/CV0-004.png', 15),
        (N'DA0-001', N'CompTIA Data+', N'Data', N'Annual', 399.90, N'CompTIA', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/DA0-001.png', 0),
        (N'DS0-001', N'CompTIA DataSys+', N'Data Platforms', N'Annual', 399.90, N'CompTIA', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/DS0-001.png', 20),
        (N'GCP-ACE', N'Google Cloud Associate Cloud Engineer', N'Cloud Administration', N'Annual', 399.90, N'Google', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/GCP-ACE.png', 20),
        (N'GCP-PCA', N'Google Cloud Professional Cloud Architect', N'Cloud Architecture', N'Annual', 599.90, N'Google', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/GCP-PCA.png', 25),
        (N'GCP-PDE', N'Google Cloud Professional Data Engineer', N'Data Engineering', N'Monthly', 59.99, N'Google', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/GCP-PDE.png', 60),
        (N'GCP-PCSE', N'Google Cloud Professional Cloud Security Engineer', N'Cloud Security', N'Monthly', 59.99, N'Google', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/GCP-PCSE.png', 60),
        (N'GCP-PCDE', N'Google Cloud Professional Cloud Developer', N'Cloud Development', N'Monthly', 59.99, N'Google', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/GCP-PCDE.png', 35),
        (N'GCP-PCDBe', N'Google Cloud Professional Cloud Database Engineer', N'Database', N'Monthly', 59.99, N'Google', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/GCP-PCDBe.png', 36),
        (N'GCP-PCNE', N'Google Cloud Professional Cloud Network Engineer', N'Networking', N'Monthly', 59.99, N'Google', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/GCP-PCNE.png', 20),
        (N'GCP-PCDVE', N'Google Cloud Professional Cloud DevOps Engineer', N'DevOps', N'Monthly', 59.99, N'Google', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/GCP-PCDVE.png', 15),
        (N'GCP-PMLE', N'Google Cloud Professional Machine Learning Engineer', N'Machine Learning', N'Annual', 599.90, N'Google', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/GCP-PMLE.png', 0),
        (N'GCP-GACE-R', N'Google Cloud Associate Cloud Engineer Renewal', N'Cloud Administration', N'Monthly', 19.99, N'Google', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/GCP-GACE-R.png', 36),
        (N'GCP-PCA-R', N'Google Cloud Professional Cloud Architect Renewal', N'Cloud Architecture', N'Monthly', 19.99, N'Google', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/GCP-PCA-R.png', 0),
        (N'ISC2-CC', N'ISC2 Certified in Cybersecurity (CC)', N'Security Fundamentals', N'Annual', 199.90, N'ISC2', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ISC2-CC.png', 40),
        (N'ISC2-SSCP', N'ISC2 Systems Security Certified Practitioner (SSCP)', N'Cybersecurity', N'Annual', 399.90, N'ISC2', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ISC2-SSCP.png', 15),
        (N'ISC2-CISSP', N'ISC2 Certified Information Systems Security Professional (CISSP)', N'Cybersecurity', N'Monthly', 69.99, N'ISC2', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ISC2-CISSP.png', 36),
        (N'ISC2-CCSP', N'ISC2 Certified Cloud Security Professional (CCSP)', N'Cloud Security', N'Monthly', 69.99, N'ISC2', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ISC2-CCSP.png', 0),
        (N'ISC2-CGRC', N'ISC2 Certified in Governance, Risk and Compliance (CGRC)', N'GRC', N'Annual', 599.90, N'ISC2', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ISC2-CGRC.png', 20),
        (N'ISC2-HCISPP', N'ISC2 HealthCare Information Security and Privacy Practitioner (HCISPP)', N'GRC', N'Monthly', 59.99, N'ISC2', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ISC2-HCISPP.png', 20),
        (N'ITIL4-FND', N'ITIL 4 Foundation', N'IT Service Management', N'Monthly', 19.99, N'ITIL', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ITIL4-FND.png', 35),
        (N'ITIL4-DSV', N'ITIL 4 Specialist: Drive Stakeholder Value', N'IT Service Management', N'Monthly', 39.99, N'ITIL', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ITIL4-DSV.png', 0),
        (N'ITIL4-HVIT', N'ITIL 4 Specialist: High-velocity IT', N'IT Service Management', N'Monthly', 39.99, N'ITIL', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ITIL4-HVIT.png', 36),
        (N'ITIL4-CDS', N'ITIL 4 Specialist: Create, Deliver and Support', N'IT Service Management', N'Annual', 399.90, N'ITIL', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ITIL4-CDS.png', 36),
        (N'ITIL4-DPI', N'ITIL 4 Strategist: Direct, Plan and Improve', N'IT Service Management', N'Annual', 399.90, N'ITIL', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ITIL4-DPI.png', 50),
        (N'ITIL4-SLD', N'ITIL 4 Leader: Digital and IT Strategy', N'IT Leadership', N'Annual', 599.90, N'ITIL', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ITIL4-SLD.png', 10),
        (N'ITIL4-PM', N'ITIL 4 Practice Manager', N'IT Service Management', N'Monthly', 59.99, N'ITIL', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ITIL4-PM.png', 20),
        (N'ITIL4-IM', N'ITIL 4 Practitioner: Incident Management', N'IT Service Management', N'Monthly', 19.99, N'ITIL', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ITIL4-IM.png', 25),
        (N'ITIL4-CSM', N'ITIL 4 Practitioner: Change Enablement', N'IT Service Management', N'Annual', 199.90, N'ITIL', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ITIL4-CSM.png', 50),
        (N'ITIL4-SLM', N'ITIL 4 Practitioner: Service Level Management', N'IT Service Management', N'Annual', 199.90, N'ITIL', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ITIL4-SLM.png', 36),
        (N'ITIL4-CONT', N'ITIL 4 Practitioner: Continual Improvement', N'IT Service Management', N'Annual', 199.90, N'ITIL', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ITIL4-CONT.png', 30),
        (N'ITS-PY', N'Pearson IT Specialist: Python', N'Programming', N'Monthly', 39.99, N'Pearson', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ITS-PY.png', 15),
        (N'ITS-JS', N'Pearson IT Specialist: JavaScript', N'Programming', N'Annual', 399.90, N'Pearson', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ITS-JS.png', 40),
        (N'ITS-JAVA', N'Pearson IT Specialist: Java', N'Programming', N'Annual', 399.90, N'Pearson', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ITS-JAVA.png', 50),
        (N'ITS-C', N'Pearson IT Specialist: C', N'Programming', N'Annual', 399.90, N'Pearson', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ITS-C.png', 60),
        (N'ITS-CNET', N'Pearson IT Specialist: C#', N'Programming', N'Monthly', 39.99, N'Pearson', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ITS-CNET.png', 60),
        (N'ITS-HTMLCSS', N'Pearson IT Specialist: HTML and CSS', N'Web Development', N'Monthly', 19.99, N'Pearson', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ITS-HTMLCSS.png', 60),
        (N'ITS-DATA', N'Pearson IT Specialist: Data Analytics', N'Data', N'Monthly', 39.99, N'Pearson', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ITS-DATA.png', 25),
        (N'ITS-NET', N'Pearson IT Specialist: Networking', N'Networking', N'Annual', 399.90, N'Pearson', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ITS-NET.png', 10),
        (N'ITS-CYBER', N'Pearson IT Specialist: Cybersecurity', N'Cybersecurity', N'Monthly', 39.99, N'Pearson', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ITS-CYBER.png', 10),
        (N'ITS-CLOUD', N'Pearson IT Specialist: Cloud Computing', N'Cloud Fundamentals', N'Monthly', 19.99, N'Pearson', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ITS-CLOUD.png', 60),
        (N'ITS-AI', N'Pearson IT Specialist: Artificial Intelligence', N'AI Fundamentals', N'Monthly', 19.99, N'Pearson', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/ITS-AI.png', 30),
        (N'TF-003', N'HashiCorp Certified: Terraform Associate (003)', N'Infrastructure as Code', N'Monthly', 39.99, N'Terraform', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/TF-003.png', 0),
        (N'VA-003', N'HashiCorp Certified: Vault Associate (003)', N'Security Automation', N'Annual', 399.90, N'Terraform', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/VA-003.png', 25),
        (N'CA-003', N'HashiCorp Certified: Consul Associate (003)', N'Service Networking', N'Annual', 399.90, N'Terraform', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/CA-003.png', 40),
        (N'VOP-001', N'HashiCorp Certified: Vault Operations Professional', N'Security Automation', N'Monthly', 59.99, N'Terraform', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/VOP-001.png', 15),
        (N'TF-PRO', N'HashiCorp Certified: Terraform Professional', N'Infrastructure as Code', N'Annual', 599.90, N'Terraform', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/TF-PRO.png', 35),
        (N'DB-DEA', N'Databricks Certified Data Engineer Associate', N'Data Engineering', N'Monthly', 39.99, N'Databricks', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/DB-DEA.png', 50),
        (N'DB-DEP', N'Databricks Certified Data Engineer Professional', N'Data Engineering', N'Monthly', 59.99, N'Databricks', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/DB-DEP.png', 25),
        (N'DB-DAA', N'Databricks Certified Data Analyst Associate', N'Data Analytics', N'Monthly', 39.99, N'Databricks', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/DB-DAA.png', 0),
        (N'DB-MLA', N'Databricks Certified Machine Learning Associate', N'Machine Learning', N'Monthly', 39.99, N'Databricks', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/DB-MLA.png', 35),
        (N'DB-MLP', N'Databricks Certified Machine Learning Professional', N'Machine Learning', N'Annual', 599.90, N'Databricks', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/DB-MLP.png', 10),
        (N'DB-GENAI', N'Databricks Certified Generative AI Engineer', N'Generative AI', N'Annual', 599.90, N'Databricks', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/DB-GENAI.png', 0),
        (N'DB-LAKE', N'Databricks Lakehouse Fundamentals', N'Data Fundamentals', N'Annual', 199.90, N'Databricks', N'https://cdn.jsdelivr.net/gh/ivantorres89/event-driven-microservices-orders@main/assets/products/DB-LAKE.png', 50);

    ;MERGE dbo.Product AS tgt
    USING @Products AS src
        ON tgt.ExternalProductId = src.ExternalProductId
    WHEN MATCHED THEN
        UPDATE SET
            Name = src.Name,
            Category = src.Category,
            BillingPeriod = src.BillingPeriod,
            IsSubscription = 1,
            Price = src.Price,
            IsActive = 1,
            Vendor = src.Vendor,
            ImageUrl = src.ImageUrl,
            Discount = src.Discount
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (ExternalProductId, Name, Category, BillingPeriod, IsSubscription, Price, IsActive, Vendor, ImageUrl, Discount)
        VALUES (src.ExternalProductId, src.Name, src.Category, src.BillingPeriod, 1, src.Price, 1, src.Vendor, src.ImageUrl, src.Discount);
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
