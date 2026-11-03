
using MechanicShop.Application.Features.Billing.Mappers;
using MechanicShop.Domain.Workorders.Billing;
using MechanicShop.Tests.Common.Billing;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepaireTasks;
using MechanicShop.Tests.Common.WorkOrders;

using Xunit;

namespace MechanicShop.Application.UnitTests.Mappers;

public class InvoiceMapperTests
{
    // =================================
    // Invoice ToDto Tests
    // =================================

    [Fact]
    public void ToDto_ShouldMapInvoiceCorrectly()
    {
        // Arrange
        var invoice = CreateValidInvoice();

        var workOrder = invoice.WorkOrder!;
        var vehicle = workOrder.Vehicle!;
        var customer = vehicle.Customer!;

        var lineItem = invoice.LineItems.First();

        // Act
        var dto = invoice.ToDto();

        // Assert
        Assert.Equal(invoice.Id, dto.InvoiceId);
        Assert.Equal(invoice.WorkOrderId, dto.WorkOrderId);

        // Customer
        Assert.NotNull(dto.Customer);
        Assert.Equal(customer.Id, dto.Customer.CustomerId);
        Assert.Equal(customer.Name, dto.Customer.Name);
        Assert.Equal(customer.Email, dto.Customer.Email);
        Assert.Equal(customer.PhoneNumber, dto.Customer.PhoneNumber);

        // Vehicle
        Assert.NotNull(dto.Vehicle);
        Assert.Equal(vehicle.Id, dto.Vehicle.VehicleId);
        Assert.Equal(vehicle.Make, dto.Vehicle.Make);
        Assert.Equal(vehicle.Model, dto.Vehicle.Model);
        Assert.Equal(vehicle.Year, dto.Vehicle.Year);
        Assert.Equal(vehicle.LicensePlate, dto.Vehicle.LicensePlate);

        // Invoice
        Assert.Equal(invoice.IssuedAtUtc, dto.IssuedAtUtc);
        Assert.Equal(invoice.Subtotal, dto.Subtotal);
        Assert.Equal(invoice.TaxAmount, dto.TaxAmount);
        Assert.Equal(invoice.DiscountAmount, dto.DiscountAmount);
        Assert.Equal(invoice.Total, dto.Total);
        Assert.Equal(invoice.Status.ToString(), dto.PaymentStatus);

        // Invoice Items
        Assert.Single(dto.Items);

        Assert.Equal(lineItem.InvoiceId, dto.Items[0].InvoiceId);
        Assert.Equal(lineItem.LineNumber, dto.Items[0].LineNumber);
        Assert.Equal(lineItem.Description, dto.Items[0].Description);
        Assert.Equal(lineItem.Quantity, dto.Items[0].Quantity);
        Assert.Equal(lineItem.UnitPrice, dto.Items[0].UnitPrice);
        Assert.Equal(lineItem.LineTotal, dto.Items[0].LineTotal);
    }

    [Fact]
    public void ToDto_WithNullInvoice_ShouldThrowArgumentNullException()
    {
        // Arrange
        Invoice? invoice = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => invoice!.ToDto());
    }


    // =================================
    // Invoice ToDtos Tests
    // =================================

    [Fact]
    public void ToDtos_WithEmptyCollection_ShouldReturnEmptyList()
    {
        // Arrange
        var invoices = new List<Invoice>();

        // Act
        var result = invoices.ToDtos();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ToDtos_WithMultipleInvoices_ShouldMapAllInvoices()
    {
        // Arrange
        var invoice1 = CreateValidInvoice();
        var invoice2 = CreateValidInvoice();

        var invoices = new List<Invoice>
        {
            invoice1,
            invoice2
        };

        // Act
        var result = invoices.ToDtos();

        // Assert
        Assert.Equal(2, result.Count);

        Assert.Equal(invoice1.Id, result[0].InvoiceId);
        Assert.Equal(invoice1.WorkOrderId, result[0].WorkOrderId);

        Assert.Equal(invoice2.Id, result[1].InvoiceId);
        Assert.Equal(invoice2.WorkOrderId, result[1].WorkOrderId);
    }


    // =================================
    // InvoiceLineItem ToDto Tests
    // =================================

    [Fact]
    public void InvoiceLineItemToDto_ShouldMapCorrectly()
    {
        // Arrange
        var item = InvoiceLineItemFactory.CreateInvoiceLineItem(
            lineNumber: 1,
            description: "Oil Change",
            quantity: 2,
            unitPrice: 150m).Value;

        // Act
        var dto = item.ToDto();

        // Assert
        Assert.Equal(item.InvoiceId, dto.InvoiceId);
        Assert.Equal(item.LineNumber, dto.LineNumber);
        Assert.Equal(item.Description, dto.Description);
        Assert.Equal(item.Quantity, dto.Quantity);
        Assert.Equal(item.UnitPrice, dto.UnitPrice);
        Assert.Equal(item.LineTotal, dto.LineTotal);
    }


    // =================================
    // InvoiceLineItem ToDtos Tests
    // =================================

    [Fact]
    public void InvoiceLineItemToDtos_WithEmptyCollection_ShouldReturnEmptyList()
    {
        // Arrange
        var items = new List<InvoiceLineItem>();

        // Act
        var result = items.ToDtos();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void InvoiceLineItemToDtos_WithMultipleItems_ShouldMapAllItems()
    {
        // Arrange
        var item1 = InvoiceLineItemFactory.CreateInvoiceLineItem(
            lineNumber: 1,
            description: "Oil Change",
            quantity: 2,
            unitPrice: 150m).Value;

        var item2 = InvoiceLineItemFactory.CreateInvoiceLineItem(
            lineNumber: 2,
            description: "Brake Repair",
            quantity: 1,
            unitPrice: 300m).Value;

        var items = new List<InvoiceLineItem>
        {
            item1,
            item2
        };

        // Act
        var result = items.ToDtos();

        // Assert
        Assert.Equal(2, result.Count);

        for (var i = 0; i < items.Count; i++)
        {
            Assert.Equal(items[i].InvoiceId, result[i].InvoiceId);
            Assert.Equal(items[i].LineNumber, result[i].LineNumber);
            Assert.Equal(items[i].Description, result[i].Description);
            Assert.Equal(items[i].Quantity, result[i].Quantity);
            Assert.Equal(items[i].UnitPrice, result[i].UnitPrice);
            Assert.Equal(items[i].LineTotal, result[i].LineTotal);
        }
    }


    // =================================
    // Test Data Factory
    // =================================

    private static Invoice CreateValidInvoice()
    {
        var customer = CustomerFactory.CreateCustomer().Value;
        var vehicle = customer.Vehicles.First();

        // Establish navigation relationship
        vehicle.Customer = customer;

        var labor = EmployeeFactory.CreateLabor().Value;

        var repairTask = RepairTaskFactory.CreateRepairTask().Value;

        var workOrder = WorkOrderFactory.CreateWorkOrder(
            vehicleId: vehicle.Id,
            laborId: labor.Id,
            repairTasks: [repairTask]).Value;

        workOrder.Vehicle = vehicle;
        workOrder.Labor = labor;

        var lineItem = InvoiceLineItemFactory.CreateInvoiceLineItem(
            lineNumber: 1,
            description: "Test Item",
            quantity: 1,
            unitPrice: 100m).Value;

        var invoice = InvoiceFactory.CreateInvoice(
            workOrderId: workOrder.Id,
            items: [lineItem]).Value;

        invoice.WorkOrder = workOrder;

        return invoice;
    }
}

