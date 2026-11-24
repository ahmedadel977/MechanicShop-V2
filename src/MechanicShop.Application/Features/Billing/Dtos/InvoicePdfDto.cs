namespace MechanicShop.Application.Features.Billing.Dtos;

public sealed class InvoicePdfDto
{
    public required byte[] Content { get; init; }
    public required string FileName { get; init; }
    public string ContentType { get; init; } = "application/pdf";
}
