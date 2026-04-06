using System.Net.WebSockets;

namespace mcp_server_aecdm
{
	public static class Global
	{
		public static Random random = new Random();
		public static string AccessToken { get; set; }
		public static string RefreshToken { get; set; }
		public static string ClientId { get; set; }
		public static string CallbackURL { get; set; }
		public static string Scopes { get; set; }
		public static string codeVerifier { get; set; }
		public static Autodesk.Data.AECDM.Client SDKClient { get; set; }

		public static WebSocket _webSocket = null;
	}
}
