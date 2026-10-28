using System;
using System.Threading.Tasks;
using System.Windows;

namespace Negotchi
{
    internal sealed partial class NegotchiApp
    {
        private ResetCredits resetCredits;
        private ResetRedemption resetRedemption;
        private bool resetBusy, resetAccountVerified;
        private string resetAccountEmail;

        private bool PendingResetAccountMatches()
        {
            return string.IsNullOrEmpty(state.PendingResetAccountEmail) || string.IsNullOrEmpty(resetAccountEmail)
                || string.Equals(state.PendingResetAccountEmail, resetAccountEmail, StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateResetButton()
        {
            if (chat == null || state == null) return;
            bool pending = resetRedemption != null && resetRedemption.Pending;
            bool connected = accountConnected && resetAccountVerified && agent != null && agent.IsConnected;
            bool enabled = false; string label = "Reset", tip;
            if (pending)
            {
                label = "Conferir reset";
                if (!connected) tip = "Conecte e confirme a conta para conferir o resgate anterior.";
                else if (!PendingResetAccountMatches()) tip = "O pedido pendente pertence a outra conta. Entre na mesma conta para conferi-lo.";
                else { enabled = true; tip = "Conferir o pedido anterior usando o mesmo identificador de resgate."; }
            }
            else if (!connected) tip = "Conecte uma conta do Codex para consultar os resets.";
            else if (resetCredits == null || !resetCredits.AvailableCount.HasValue)
                tip = resetCredits != null && resetCredits.Error != null ? "Não foi possível consultar os resets: " + resetCredits.Error : "O Codex não informou quantos resets estão disponíveis.";
            else
            {
                int count = resetCredits.AvailableCount.Value; label = "Reset (" + count + ")";
                if (count == 0) tip = "Nenhum reset bancado do Codex disponível.";
                else if (resetCredits.Eligible == false) tip = "Para resgatar, uma janela principal precisa ter menos de 80% do saldo restante.";
                else
                {
                    enabled = true; tip = count + (count == 1 ? " reset bancado disponível." : " resets bancados disponíveis.");
                    if (!resetCredits.Eligible.HasValue) tip += " O Codex verificará se existe uma janela elegível.";
                    if (!string.IsNullOrEmpty(resetCredits.Expirations)) tip += " Vencimento informado: " + resetCredits.Expirations + ".";
                }
            }
            chat.SetResetButton(label, enabled && connected && !resetBusy, tip);
        }

        private async void RedeemReset()
        {
            if (busy || commandBusy || resetBusy || !accountConnected || !resetAccountVerified || !agent.IsConnected) return;
            bool pending = resetRedemption.Pending;
            if (pending && !PendingResetAccountMatches()) return;
            resetBusy = true; commandBusy = true; chat.SetCommandBusy(true); UpdateResetButton();
            try
            {
                if (!pending)
                {
                    var limits = await RefreshAccountLimits();
                    if (limits == null || resetCredits == null || !resetCredits.AvailableCount.HasValue)
                    {
                        ResetMessage("Não foi possível confirmar a disponibilidade dos resets. Atualize os limites e tente novamente.", false); return;
                    }
                    if (resetCredits.AvailableCount.Value == 0) { ResetMessage("O Codex não informou nenhum reset disponível.", false); return; }
                    if (resetCredits.Eligible == false) { ResetMessage("Nenhuma janela principal está abaixo de 80% de saldo restante. O reset continua disponível.", false); return; }
                }
                int account = accountRevision;
                string description;
                if (pending)
                {
                    description = "O resultado de um pedido anterior ainda não foi confirmado. Confirme que esta é a mesma conta e o mesmo espaço de trabalho usados naquele pedido. O Negotchi repetirá o mesmo identificador para consultar o resultado com segurança.";
                    if (!string.IsNullOrEmpty(state.PendingResetAccountEmail)) description += "\n\nConta informada no pedido: " + state.PendingResetAccountEmail;
                }
                else
                {
                    description = "Resgatar um reset bancado do Codex? Disponíveis: " + resetCredits.AvailableCount.Value + ".\n\nO serviço escolhe um dos resets e restaura as janelas de uso elegíveis. A próxima renovação pode mudar. O reset só é consumido se uma janela for restaurada; o histórico de tokens não é apagado.";
                    if (!string.IsNullOrEmpty(resetCredits.Expirations)) description += "\n\nVencimentos informados: " + resetCredits.Expirations;
                    if (!resetCredits.Eligible.HasValue) description += "\n\nO Codex verificará a elegibilidade no momento do pedido.";
                }
                if (!DesktopActions.Review(chat, pending ? "Conferir resgate anterior" : "Resgatar reset do Codex", description)) return;
                if (!accountConnected || !resetAccountVerified || !agent.IsConnected || account != accountRevision || (pending && !PendingResetAccountMatches()))
                {
                    ResetMessage("A conta mudou durante a confirmação. Consulte os limites e tente novamente.", false); return;
                }
                string outcome = await resetRedemption.Consume(agent, resetAccountEmail);
                var updated = await RefreshAccountLimits();
                string message;
                if (outcome == "reset" || outcome == "alreadyRedeemed") message = "Reset confirmado pelo Codex.";
                else if (outcome == "nothingToReset") message = "O Codex informou que não há janela elegível para restaurar. O reset continua disponível.";
                else if (outcome == "noCredit") message = "O Codex informou que não há reset disponível.";
                else message = "O Codex retornou um resultado não reconhecido. Use Conferir reset para repetir o mesmo pedido.";
                if (updated == null || updated.Error != null) message += " Não foi possível atualizar os limites agora.";
                ResetMessage(message, outcome == "reset" || outcome == "alreadyRedeemed");
            }
            catch (Exception error)
            {
                string message = resetRedemption.Pending
                    ? "Não foi possível confirmar o resultado. Use Conferir reset na mesma conta e no mesmo espaço de trabalho para repetir o mesmo pedido. "
                    : "Não foi possível iniciar o resgate. ";
                ResetMessage(message + error.Message, false);
            }
            finally { resetBusy = false; commandBusy = false; chat.SetCommandBusy(false); UpdateResetButton(); }
        }

        private void ResetMessage(string message, bool success)
        {
            chat.SetStatus(message);
            MessageBox.Show(chat, message, "Reset do Codex", MessageBoxButton.OK, success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
    }
}
