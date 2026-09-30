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

		public byte[] Generate(
			Rental rental,
			Payment? payment,
			IReadOnlyList<RentalVehicle>? rentalVehicles = null)
		{
			var lines = ResolveVehicleLines(rental, rentalVehicles);
			var bookingLabel = $"BK-{rental.RentalId:D5}";
			var issuedAt = DateTime.Now;
			var hasDiscount = rental.Discount == Discount.Yes;
			var subtotal = lines.Sum(l => l.LineTotalAmount);
			var totalFare = rental.TotalAmount > 0
				? rental.TotalAmount
				: (payment?.TotalAmount ?? subtotal);
			var discountAmount = hasDiscount
				? Math.Max(0, subtotal - totalFare)
				: 0m;

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

						col.Item().Text(lines.Count > 1 ? "Vehicles" : "Trip details").FontSize(12).Bold();
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

							if (lines.Count <= 1)
							{
								var line = lines.FirstOrDefault();
								var vehicle = line?.Vehicle;
								var vehicleTitle = vehicle == null
									? "—"
									: $"{vehicle.Brand} {vehicle.Model}".Trim();
								Row("Vehicle", vehicleTitle);
								if (vehicle != null)
								{
									Row("Type / Plate", $"{vehicle.Type} · {vehicle.PlateNumber}");
								}
							}
							else
							{
								for (var i = 0; i < lines.Count; i++)
								{
									var line = lines[i];
									var vehicle = line.Vehicle;
									var vehicleTitle = vehicle == null
										? $"Vehicle #{line.VehicleId}"
										: $"{vehicle.Brand} {vehicle.Model}".Trim();
									var plate = vehicle?.PlateNumber ?? "—";
									var type = vehicle?.Type ?? "—";
									Row($"Vehicle {i + 1}", $"{vehicleTitle} ({type} · {plate})");
								}
							}

							Row("Pickup", $"{rental.PickupLocation} — {rental.PickupDate:MMM d, yyyy} {rental.PickupTime:h:mm tt}");
							Row("Return", $"{rental.DropoffLocation} — {rental.ReturnDate:MMM d, yyyy} {rental.ReturnTime:h:mm tt}");
							Row("Passengers", rental.PassengerCount.ToString());
						});

						if (lines.Count > 0)
						{
							col.Item().PaddingTop(8).Text("Fare breakdown").FontSize(12).Bold();
							col.Item().Table(table =>
							{
								table.ColumnsDefinition(columns =>
								{
									columns.RelativeColumn(2f);
									columns.RelativeColumn(1f);
								});

								void AmountRow(string label, string amount, bool emphasize = false)
								{
									var labelCell = table.Cell().PaddingVertical(3).Text(label);
									var amountCell = table.Cell().PaddingVertical(3).AlignRight().Text(amount);
									if (emphasize)
									{
										labelCell.SemiBold();
										amountCell.Bold();
									}
									else
									{
										labelCell.FontColor(Colors.Grey.Medium);
										amountCell.SemiBold();
									}
								}

								foreach (var line in lines)
								{
									var vehicle = line.Vehicle;
									var label = vehicle == null
										? $"Vehicle #{line.VehicleId}"
										: $"{vehicle.Brand} {vehicle.Model}".Trim();
									var detail = FormatLineFareDetail(line);
									AmountRow(label, $"{FormatPeso(line.LineTotalAmount)} ({detail})");
								}

								if (lines.Count > 1)
								{
									AmountRow("Subtotal", FormatPeso(subtotal));
								}

								if (hasDiscount && discountAmount > 0)
								{
									AmountRow("Senior/PWD discount (10%)", $"-{FormatPeso(discountAmount)}");
								}

								AmountRow("Total fare", FormatPeso(totalFare), emphasize: true);
							});
						}

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
								Row("Total fare", FormatPeso(totalFare));
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

		private static List<RentalVehicle> ResolveVehicleLines(
			Rental _,
			IReadOnlyList<RentalVehicle>? rentalVehicles)
		{
			return rentalVehicles?
				.OrderBy(rv => rv.SortOrder)
				.ThenBy(rv => rv.RentalVehicleId)
				.ToList() ?? new List<RentalVehicle>();
		}

		private static string FormatLineFareDetail(RentalVehicle line)
		{
			if (line.LineSucceedingFeeTotal > 0)
			{
				return $"base {FormatPeso(line.LineBaseAmount)} + exceeding {FormatPeso(line.LineSucceedingFeeTotal)}";
			}

			return $"base {FormatPeso(line.LineBaseAmount)}";
		}

		private static string FormatPeso(decimal amount) =>
			"PHP " + Math.Max(0, amount).ToString("N2", Ph);
	}
}
