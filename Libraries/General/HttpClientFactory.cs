using System.Net;

namespace DDS.General;

internal static class HttpClientFactory
{
	// readonly, no lazy: dos llamadas simultáneas a la versión con ??= creaban dos handlers y todos menos uno
	// quedaban colgados con su propio pool de conexiones.
	private static readonly SocketsHttpHandler _httpMessageHandler = InstanceFactory();

	public static HttpClient CreateHttpClient()
	{
		return new(_httpMessageHandler, false);
	}

	private static SocketsHttpHandler InstanceFactory()
	{
		return new()
		{
			AutomaticDecompression = DecompressionMethods.All,
			PooledConnectionLifetime = TimeSpan.FromMinutes(5),
			AllowAutoRedirect = false
		};
	}
}
