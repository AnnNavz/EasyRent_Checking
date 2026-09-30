using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace EasyRent_Checking.ViewModels
{
	[AttributeUsage(AttributeTargets.Property)]
	public sealed class MustBeTrueAttribute : ValidationAttribute, IClientModelValidator
	{
		public override bool IsValid(object? value) => value is true;

		public void AddValidation(ClientModelValidationContext context)
		{
			ArgumentNullException.ThrowIfNull(context);
			context.Attributes["data-val"] = "true";
			context.Attributes["data-val-required"] = FormatErrorMessage(context.ModelMetadata.GetDisplayName());
		}
	}
}
