using System.Text.Json;

namespace TwitchBot.ElevenLabs
{
    public record VoiceModel(
        string model_id,
        string display_name,
        List<SupportedLanguage> supported_languages
    );

    public record SupportedLanguage(
        string iso_code,
        string display_name
    );

    public record Invoice(
        long amount_due_cents,
        long next_payment_attempt_unix
    );

    public record SubscriptionInfoResponse(
        string tier,
        long character_count,
        long character_limit,
        bool can_extend_character_limit,
        bool allowed_to_extend_character_limit,
        long next_character_count_reset_unix,
        int voice_limit,
        bool can_extend_voice_limit,
        bool can_use_instant_voice_cloning,
        List<VoiceModel> available_models,
        string status,
        Invoice next_invoice
    );

    internal class SubscriptionInfo
    {
        const string URL = "https://api.elevenlabs.io/v1/user/subscription";
        public static SubscriptionInfoResponse call(HttpClient client)
        {
            var request = buildSubscriptionInfoRequest();
            var response = client.Send(request);
            var jsonResponse = JsonSerializer.Deserialize<SubscriptionInfoResponse>(response.Content.ReadAsStream());
            return jsonResponse;
        }

        public static HttpRequestMessage buildSubscriptionInfoRequest()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, URL);
            request.Headers.Add("Accept", "application/json");
            return request;
        }
    }
}
