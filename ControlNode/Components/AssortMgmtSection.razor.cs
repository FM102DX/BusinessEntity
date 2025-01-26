using Microsoft.AspNetCore.Components;

namespace ControlNode.Components
{
    public partial class AssortMgmtSection
    {
        [Parameter]
        public string Title { get; set; } = string.Empty;

        [Parameter]
        public RenderFragment ChildContent { get; set; }
    }
}
