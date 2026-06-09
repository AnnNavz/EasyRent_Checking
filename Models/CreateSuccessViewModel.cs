namespace EasyRent_Checking.Models
{
    public class CreateSuccessViewModel
    {
        public string PageTitle { get; set; } = "Success";
        public string ActivePage { get; set; } = "";
        public string Heading { get; set; } = "";
        public string MessageHtml { get; set; } = "";
        public string PrimaryActionText { get; set; } = "";
        public string PrimaryActionUrl { get; set; } = "";
        public string SecondaryActionText { get; set; } = "";
        public string SecondaryActionUrl { get; set; } = "";
    }
}
