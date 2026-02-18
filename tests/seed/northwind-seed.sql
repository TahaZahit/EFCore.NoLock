-- Northwind DB Seed (minimal subset for integration testing)
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'Northwind')
    CREATE DATABASE Northwind;
GO

USE Northwind;
GO

-- Categories
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Categories')
CREATE TABLE Categories (
    CategoryID   INT           IDENTITY(1,1) PRIMARY KEY,
    CategoryName NVARCHAR(15)  NOT NULL,
    Description  NTEXT         NULL
);
GO

-- Suppliers
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Suppliers')
CREATE TABLE Suppliers (
    SupplierID   INT           IDENTITY(1,1) PRIMARY KEY,
    CompanyName  NVARCHAR(40)  NOT NULL,
    ContactName  NVARCHAR(30)  NULL,
    City         NVARCHAR(15)  NULL,
    Country      NVARCHAR(15)  NULL
);
GO

-- Products
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Products')
CREATE TABLE Products (
    ProductID    INT           IDENTITY(1,1) PRIMARY KEY,
    ProductName  NVARCHAR(40)  NOT NULL,
    SupplierID   INT           NULL REFERENCES Suppliers(SupplierID),
    CategoryID   INT           NULL REFERENCES Categories(CategoryID),
    UnitPrice    MONEY         NULL DEFAULT 0,
    UnitsInStock SMALLINT      NULL DEFAULT 0,
    Discontinued BIT           NOT NULL DEFAULT 0
);
GO

-- Customers
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Customers')
CREATE TABLE Customers (
    CustomerID  NCHAR(5)      PRIMARY KEY,
    CompanyName NVARCHAR(40)  NOT NULL,
    ContactName NVARCHAR(30)  NULL,
    City        NVARCHAR(15)  NULL,
    Country     NVARCHAR(15)  NULL
);
GO

-- Orders
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Orders')
CREATE TABLE Orders (
    OrderID     INT           IDENTITY(1,1) PRIMARY KEY,
    CustomerID  NCHAR(5)      NULL REFERENCES Customers(CustomerID),
    OrderDate   DATETIME      NULL,
    ShipCity    NVARCHAR(15)  NULL,
    ShipCountry NVARCHAR(15)  NULL
);
GO

-- Order Details
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OrderDetails')
CREATE TABLE OrderDetails (
    OrderID     INT   NOT NULL REFERENCES Orders(OrderID),
    ProductID   INT   NOT NULL REFERENCES Products(ProductID),
    UnitPrice   MONEY NOT NULL DEFAULT 0,
    Quantity    SMALLINT NOT NULL DEFAULT 1,
    Discount    REAL  NOT NULL DEFAULT 0,
    PRIMARY KEY (OrderID, ProductID)
);
GO

-- Seed Categories
SET IDENTITY_INSERT Categories ON;
INSERT INTO Categories (CategoryID, CategoryName, Description) VALUES
(1, N'Beverages',    N'Soft drinks, coffees, teas, beers, and ales'),
(2, N'Condiments',   N'Sweet and savory sauces, relishes, spreads, and seasonings'),
(3, N'Dairy Products', N'Cheeses'),
(4, N'Seafood',      N'Seaweed and fish');
SET IDENTITY_INSERT Categories OFF;
GO

-- Seed Suppliers
SET IDENTITY_INSERT Suppliers ON;
INSERT INTO Suppliers (SupplierID, CompanyName, ContactName, City, Country) VALUES
(1, N'Exotic Liquids', N'Charlotte Cooper', N'London', N'UK'),
(2, N'New Orleans Cajun Delights', N'Shelley Burke', N'New Orleans', N'USA'),
(3, N'Tokyo Traders', N'Yoshi Nagase', N'Tokyo', N'Japan');
SET IDENTITY_INSERT Suppliers OFF;
GO

-- Seed Products
SET IDENTITY_INSERT Products ON;
INSERT INTO Products (ProductID, ProductName, SupplierID, CategoryID, UnitPrice, UnitsInStock, Discontinued) VALUES
(1,  N'Chai',              1, 1, 18.00, 39, 0),
(2,  N'Chang',             1, 1, 19.00, 17, 0),
(3,  N'Aniseed Syrup',     1, 2, 10.00, 13, 0),
(4,  N'Ikura',             3, 4, 31.00, 31, 0),
(5,  N'Tofu',              3, 3, 23.25, 35, 0);
SET IDENTITY_INSERT Products OFF;
GO

-- Seed Customers
INSERT INTO Customers (CustomerID, CompanyName, ContactName, City, Country) VALUES
(N'ALFKI', N'Alfreds Futterkiste',     N'Maria Anders',   N'Berlin',  N'Germany'),
(N'ANATR', N'Ana Trujillo Emparedados', N'Ana Trujillo',   N'México',  N'Mexico'),
(N'ANTON', N'Antonio Moreno Taquería',  N'Antonio Moreno', N'México',  N'Mexico');
GO

-- Seed Orders
SET IDENTITY_INSERT Orders ON;
INSERT INTO Orders (OrderID, CustomerID, OrderDate, ShipCity, ShipCountry) VALUES
(10248, N'ALFKI', '1996-07-04', N'Berlin',  N'Germany'),
(10249, N'ANATR', '1996-07-05', N'México',  N'Mexico'),
(10250, N'ANTON', '1996-07-08', N'México',  N'Mexico');
SET IDENTITY_INSERT Orders OFF;
GO

-- Seed Order Details
INSERT INTO OrderDetails (OrderID, ProductID, UnitPrice, Quantity, Discount) VALUES
(10248, 1, 18.00, 12, 0),
(10248, 2, 19.00, 10, 0),
(10249, 3, 10.00, 9,  0),
(10249, 4, 31.00, 40, 0.25),
(10250, 5, 23.25, 10, 0);
GO
