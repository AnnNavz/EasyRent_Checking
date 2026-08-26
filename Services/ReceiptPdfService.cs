using System.Globalization;
using EasyRent_Checking.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EasyRent_Checking.Services
{
	public class ReceiptPdfService
	{
		private static readonly CultureInfo Ph = CultureInfo.GetCultureInfo("en-PH");

		public byte[] Generate(Rental rental, RentalDetails? details, Payment? payment, Vehicle? vehicle = null)
		{
			vehicle ??= details?.Vehicle;
			var bookingLabel = $"BK-{rental.RentalId:D5}";
			var issuedAt = DateTime.Now;
			var vehicleTitle = vehicle == null
				? "—"
				: $"{vehicle.Brand} {vehicle.Model}".Trim();

			var document = Document.Create(container =>
			{
				container.Page(page =>
				{
					page.Size(PageSizes.A4);
					page.Margin(40);
					page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

					page.Header().Column(col =>
					{
						col.Item().Text("EasyRent").FontSize(22).Bold().FontColor(Colors.Blue.Darken3);
						col.Item().Text("Official Payment Receipt").FontSize(12).SemiBold();
						col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
					});

					page.Content().PaddingVertical(16).Column(col =>
					{
						col.Spacing(10);

						col.Item().Row(row =>
						{
							row.RelativeItem().Column(c =>
							{
								c.Item().Text("Receipt for").FontSize(8).FontColor(Colors.Grey.Medium);
								c.Item().Text(bookingLabel).FontSize(14).Bold();
							});
							row.RelativeItem().AlignRight().Column(c =>
							{
								c.Item().Text("Issued").FontSize(8).FontColor(Colors.Grey.Medium);
								c.Item().Text(issuedAt.ToString("MMM d, yyyy h:mm tt")).SemiBold();
							});
						});

						col.Item().Background(Colors.Grey.Lighten4).Padding(12).Column(box =>
						{
							box.Spacing(4);
							box.Item().Text("Booking Status: Confirmed").Bold().FontColor(Colors.Green.Darken2);
							box.Item().Text($"Customer: {rental.CustomerName}");
							box.Item().Text($"Contact: {rental.ContactNumber}");
						});

						col.Item().Text("Trip details").FontSize(12).Bold();
						col.Item().Table(table =>
						{
							table.ColumnsDefinition(columns =>
							{
								columns.RelativeColumn(1.2f);
								columns.RelativeColumn(2f);
							});

							void Row(string label, string value)
							{
								table.Cell().PaddingVertical(3).Text(label).FontColor(Colors.Grey.Medium);
								table.Cell().PaddingVertical(3).Text(value).SemiBold();
							}

							Row("Vehicle", vehicleTitle);
							if (vehicle != null)
							{
								Row("Type / Plate", $"{vehicle.Type} · {vehicle.PlateNumber}");
							}

							if (details != null)
							{
								Row("Pickup", $"{details.PickupLocation} — {details.PickupDate:MMM d, yyyy} {details.PickupTime:h:mm tt}");
								Row("Return", $"{details.DropoffLocation} — {details.ReturnDate:MMM d, yyyy} {details.ReturnTime:h:mm tt}");
								Row("Passengers", details.PassengerCount.ToString());
							}
						});

						col.Item().PaddingTop(8).Text("Payment details").FontSize(12).Bold();
						if (payment == null)
						{
							col.Item().Text("No payment record on file.").Italic();
						}
						else
						{
							col.Item().Table(table =>
							{
								table.ColumnsDefinition(columns =>
								{
									columns.RelativeColumn(1.2f);
									columns.RelativeColumn(2f);
								});

								void Row(string label, string value)
								{
									table.Cell().PaddingVertical(3).Text(label).FontColor(Colors.Grey.Medium);
									table.Cell().PaddingVertical(3).Text(value).SemiBold();
								}

								Row("Payment type", payment.PaymentType);
								Row("Payment method", payment.PaymentMethod);
								Row("Payment date", payment.PaymentDate.ToString("MMM d, yyyy h:mm tt"));
								Row("Amount paid", FormatPeso(payment.AmountPaid));
								Row("Total fare", FormatPeso(payment.TotalAmount));
								if (!string.IsNullOrWhiteSpace(payment.AccountName))
								{
									Row("Account name", payment.AccountName);
								}

								if (!string.IsNullOrWhiteSpace(payment.TransactionReference))
								{
									Row("Reference no.", payment.TransactionReference);
								}
							});
						}

						col.Item().PaddingTop(12).Background(Colors.Blue.Darken3).Padding(12).Row(row =>
						{
							row.RelativeItem().Text("Amount acknowledged")
								.FontColor(Colors.White).SemiBold();
							row.RelativeItem().AlignRight()
								.Text(FormatPeso(payment?.AmountPaid ?? 0m))
								.FontColor(Colors.White).FontSize(14).Bold();
						});

						col.Item().PaddingTop(10).Text(
							"This receipt confirms that EasyRent has verified your payment and approved your booking. Keep this file for your records.")
							.FontSize(9).FontColor(Colors.Grey.Medium);
					});

					page.Footer().AlignCenter().Text(text =>
					{
						text.Span("EasyRent · ").FontColor(Colors.Grey.Medium);
						text.Span(bookingLabel).SemiBold();
						text.Span(" · Page ").FontColor(Colors.Grey.Medium);
						text.CurrentPageNumber();
					});
				});
			});

			return document.GeneratePdf();
		}

		private static string FormatPeso(decimal amount) =>
			"PHP " + Math.Max(0, amount).ToString("N2", Ph);
	}
}
