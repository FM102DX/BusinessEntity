using Microsoft.AspNetCore.Components;

namespace BusinessEntity.Components
{
    public partial class LoggerWidget
    {
        [Parameter]
        public string Title { get; set; } = string.Empty;

        [Parameter]
        public RenderFragment ChildContent { get; set; }
    }
}
