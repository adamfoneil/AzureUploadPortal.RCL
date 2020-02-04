using Microsoft.AspNetCore.Components;

namespace BlazorSample.Shared
{
    public class ToggleThemeAlertBase : ComponentBase
    {
        [Parameter]
        public RenderFragment ChildContent { get; set; }

        protected bool IsDark;
        protected string Theme => IsDark ? "dark" : "light";

        protected override void OnInitialized()
        {
            base.OnInitialized();
            IsDark = true;
        }
    }
}
