using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Negotchi
{
    internal static partial class Tests
    {
        private static RpcRead ResetLimits(object count, object primary, object secondary)
        {
            return new RpcRead { Data = Json.Map("rateLimitResetCredits", Json.Map("availableCount", count, "credits", null),
                "rateLimitsByLimitId", Json.Map("codex", Json.Map("primary", primary, "secondary", secondary),
                    "other", Json.Map("primary", Json.Map("usedPercent", 99)))) };
        }

        private static void TestResetCredits()
        {
            var unknown = ResetCredits.FromRead(new RpcRead { Data = Json.Map() });
            Check(!unknown.AvailableCount.HasValue, "saldo ausente não é tratado como zero");
            var unavailable = ResetCredits.FromRead(new RpcRead { Error = "offline" });
            Check(!unavailable.AvailableCount.HasValue && unavailable.Error == "offline", "falha de consulta preserva o motivo");
            var blocked = ResetCredits.FromRead(ResetLimits(2, Json.Map("usedPercent", 20), null));
            Check(blocked.AvailableCount == 2 && blocked.Eligible == false, "80% restante não habilita resgate e outro limite não interfere");
            var eligible = ResetCredits.FromRead(ResetLimits(2, Json.Map("usedPercent", 20.1), null));
            Check(eligible.AvailableCount == 2 && eligible.Eligible == true, "menos de 80% restante habilita resgate");
            var missing = ResetCredits.FromRead(ResetLimits(1, Json.Map("usedPercent", null), null));
            Check(missing.AvailableCount == 1 && !missing.Eligible.HasValue, "dados incompletos deixam elegibilidade para o serviço decidir");
            var empty = ResetCredits.FromRead(ResetLimits(0, Json.Map("usedPercent", 60), null));
            Check(empty.AvailableCount == 0 && empty.Eligible == true, "saldo zero é distinto de dados ausentes");
            var partial = ResetCredits.FromRead(new RpcRead { Data = Json.Map("rateLimitResetCredits", Json.Map("availableCount", 3, "credits", new object[0]),
                "rateLimits", Json.Map("primary", Json.Map("usedPercent", 70))) });
            Check(partial.AvailableCount == 3 && partial.Eligible == true, "contagem é autoritativa mesmo com lista de detalhes vazia");

            var chat = new ChatWindow(new PetAssets());
            chat.SetResetButton("Reset (2)", true, "Dois resets");
            Render((FrameworkElement)chat.Content, 414, 481, Path.Combine(output, "reset-conversa.png"));
            var button = FindButton((FrameworkElement)chat.Content, "Reset (2)");
            Check(button != null && button.IsEnabled && ButtonFits((FrameworkElement)chat.Content, "Reset (2)"), "botão de reset cabe junto das abas na largura mínima");
            chat.SetBusy(true); Check(!button.IsEnabled, "resgate fica indisponível durante um turno");
            chat.SetBusy(false); chat.ShowPets();
            Render((FrameworkElement)chat.Content, 414, 481, Path.Combine(output, "reset-pets.png"));
            Check(FindButton((FrameworkElement)chat.Content, "Reset (2)") != null, "botão permanece visível na aba Pets");
            chat.CloseForExit();
        }

        private static async Task TestResetProtocol()
        {
            var state = new AppState(); int writes = 0; string first = null, second = null;
            var operation = new ResetRedemption(state, delegate { writes++; });
            bool failed = false;
            try
            {
                await operation.Consume(delegate(string key) {
                    first = key; var task = new TaskCompletionSource<Dictionary<string, object>>();
                    task.SetException(new InvalidOperationException("resposta perdida")); return task.Task;
                }, "conta@example.com");
            }
            catch (InvalidOperationException) { failed = true; }
            Guid parsed;
            Check(failed && Guid.TryParse(first, out parsed) && state.PendingResetIdempotencyKey == first && writes == 1, "falha preserva chave persistida antes da chamada");
            var reopened = new ResetRedemption(state, delegate { writes++; });
            string outcome = await reopened.Consume(delegate(string key) { second = key; return Task.FromResult(Json.Map("outcome", "alreadyRedeemed")); }, "conta@example.com");
            Check(outcome == "alreadyRedeemed" && first == second && state.PendingResetIdempotencyKey == null && writes == 2, "retentativa após reiniciar usa a mesma chave e limpa após confirmação");
            var incomplete = new ResetRedemption(state, delegate { writes++; });
            await incomplete.Consume(delegate(string key) { return Task.FromResult(Json.Map("outcome", "outro")); }, "conta@example.com");
            Check(incomplete.Pending, "resultado desconhecido mantém pedido pendente");

            string previous = Environment.GetEnvironmentVariable("NEGOTCHI_CODEX_PATH");
            Environment.SetEnvironmentVariable("NEGOTCHI_CODEX_PATH", Assembly.GetExecutingAssembly().Location);
            try
            {
                using (var client = new CodexClient())
                {
                    await client.StartAsync(output);
                    var remote = new ResetRedemption(new AppState(), delegate { });
                    outcome = await remote.Consume(client, "conta@example.com");
                    var captured = Json.Object(await client.RequestAsync("test/state", null), "lastReset");
                    Check(outcome == "noCredit" && !remote.Pending && Guid.TryParse(Json.Text(captured, "idempotencyKey"), out parsed) && !captured.ContainsKey("creditId"), "usa operação nativa com UUID e deixa o serviço escolher o reset");
                }
            }
            finally { Environment.SetEnvironmentVariable("NEGOTCHI_CODEX_PATH", previous); }
        }
    }
}
