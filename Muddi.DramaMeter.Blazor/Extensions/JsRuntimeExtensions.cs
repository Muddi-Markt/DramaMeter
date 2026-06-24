using Microsoft.JSInterop;

namespace Muddi.DramaMeter.Blazor.Extensions;

public static class JsRuntimeExtensions
{
	extension(IJSRuntime jsRuntime)
	{
		public ValueTask LogInformationAsync(string message)
		{
			return jsRuntime.InvokeVoidAsync("console.log", message);
		}

		public ValueTask LogErrorAsync(string message)
		{
			return jsRuntime.InvokeVoidAsync("console.error", message);
		}
	}
}