namespace FileFlow.Client.Components
{
    using Microsoft.AspNetCore.Components;
    using FileFlow.Client.Shared;

    public partial class PageView
    {
        [CascadingParameter]
        public NavMenu Menu { get; set; }


        [Parameter]
        public RenderFragment Head { get; set; }

        [Parameter]
        public RenderFragment Body { get; set; }

    }
}