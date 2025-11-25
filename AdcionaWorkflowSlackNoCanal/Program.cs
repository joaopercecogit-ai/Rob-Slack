using System.Net.Http.Headers;
using System.Text.Json;

class Program
{
    // OAuth token do APP que já está no canal
    private static readonly string oauthToken = "";

    // ID do Bot/App que você quer adicionar (Ex: U05354ABCDE)

    private static readonly string botUserId = "";

    private static readonly HttpClient client = new HttpClient();

    static async Task Main()
    {
        Console.WriteLine("📡 Iniciando varredura para adicionar o APP aos canais...\n");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oauthToken);

        try
        {
            // 1. Busca canais (públicos e privados onde o dono do token está)
            var todosCanais = await GetAllChannelsAsync();
            Console.WriteLine($"✅ {todosCanais.Count} canais identificados.\n");

            Console.WriteLine($"⚠️ ATENÇÃO: O App {botUserId} será adicionado a TODOS os canais listados em 5 segundos...");
            await Task.Delay(5000);

            int contador = 1;
            foreach (var canal in todosCanais)
            {
                Console.WriteLine($"[{contador}/{todosCanais.Count}] ➕ Adicionando App ao canal: {canal.name} ({canal.id})...");

                await InviteAppToChannelAsync(canal.id, botUserId);

                // Delay para evitar Rate Limiting (Tier 2 do Slack é aprox 20 req/min para invites)
                await Task.Delay(1500); // Aumentei o delay pois 'invite' é mais restrito que 'postMessage'
                contador++;
            }

            Console.WriteLine("\n✅ Processo concluído!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro fatal: {ex.Message}");
        }
    }

    // ---------------------------
    // Função: Buscar canais
    // ---------------------------
    static async Task<List<(string id, string name)>> GetAllChannelsAsync()
    {
        var allChannels = new List<(string, string)>();
        string cursor = "";
        bool hasMore;

        do
        {
            string url = $"https://slack.com/api/conversations.list?types=public_channel,private_channel&exclude_archived=true&limit=200";
            if (!string.IsNullOrEmpty(cursor)) url += $"&cursor={cursor}";

            var response = await client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);

            if (!doc.RootElement.GetProperty("ok").GetBoolean())
                throw new Exception("Erro ao listar canais: " + response);

            var channels = doc.RootElement.GetProperty("channels");
            foreach (var ch in channels.EnumerateArray())
            {
                string id = ch.GetProperty("id").GetString();
                string name = ch.GetProperty("name").GetString();
                allChannels.Add((id, name));
            }

            hasMore = doc.RootElement.TryGetProperty("response_metadata", out var meta)
                      && meta.TryGetProperty("next_cursor", out var nc)
                      && !string.IsNullOrEmpty(nc.GetString());

            cursor = hasMore ? meta.GetProperty("next_cursor").GetString() : "";
            await Task.Delay(200);
        }
        while (hasMore);

        return allChannels;
    }

    // ---------------------------
    // Função: Convidar App (conversations.invite)
    // ---------------------------
    static async Task InviteAppToChannelAsync(string channelId, string userIdToInvite)
    {
        try
        {
            var payload = new Dictionary<string, string>
            {
                ["channel"] = channelId,
                ["users"] = userIdToInvite // ID do Bot (ex: U123456)
            };

            var content = new FormUrlEncodedContent(payload);
            var response = await client.PostAsync("https://slack.com/api/conversations.invite", content);
            var result = await response.Content.ReadAsStringAsync();

            // Tratamento de resposta
            if (result.Contains("\"ok\":true"))
            {
                Console.WriteLine("   -> ✅ Sucesso: App adicionado.");
            }
            else if (result.Contains("already_in_channel"))
            {
                Console.WriteLine("   -> 🔸 Info: O App já estava neste canal.");
            }
            else if (result.Contains("not_in_channel"))
            {
                Console.WriteLine("   -> ❌ Erro: Você (dono do token) precisa estar no canal para convidar o bot.");
            }
            else
            {
                Console.WriteLine($"   -> ❌ Falha: {result}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   -> ❌ Erro de conexão: {ex.Message}");
        }
    }
}