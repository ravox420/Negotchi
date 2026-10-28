using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Negotchi
{
    internal sealed class ResetCredits
    {
        public int? AvailableCount;
        public bool? Eligible;
        public string Expirations;
        public string Error;

        public static ResetCredits FromRead(RpcRead read)
        {
            var result = new ResetCredits();
            if (read == null || read.Error != null) { result.Error = read == null ? "Consulta não realizada." : read.Error; return result; }
            var credits = Json.Object(read.Data, "rateLimitResetCredits");
            double count;
            if (Number(Json.Get(credits, "availableCount"), out count) && count <= int.MaxValue && count == Math.Floor(count))
                result.AvailableCount = (int)count;
            result.Eligible = Eligibility(read.Data);
            var dates = Json.Items(credits, "credits").OfType<Dictionary<string, object>>()
                .Where(item => string.Equals(Json.Text(item, "status"), "available", StringComparison.OrdinalIgnoreCase))
                .Select(item => Expiration(Json.Get(item, "expiresAt"))).Where(date => date.Length > 0).Distinct().Take(3).ToArray();
            result.Expirations = string.Join(" · ", dates);
            return result;
        }

        private static bool Number(object value, out double number)
        {
            number = 0;
            return value != null && double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out number)
                && !double.IsNaN(number) && !double.IsInfinity(number) && number >= 0;
        }

        private static bool? Eligibility(Dictionary<string, object> data)
        {
            Dictionary<string, object> snapshot = null;
            var buckets = Json.Object(data, "rateLimitsByLimitId");
            var bucket = buckets.FirstOrDefault(entry => string.Equals(entry.Key, "codex", StringComparison.OrdinalIgnoreCase));
            if (bucket.Value != null) snapshot = bucket.Value as Dictionary<string, object>;
            if (snapshot == null)
            {
                var legacy = Json.Object(data, "rateLimits"); string id = Json.Text(legacy, "limitId");
                if (legacy.Count == 0 || (id.Length > 0 && !string.Equals(id, "codex", StringComparison.OrdinalIgnoreCase))) return null;
                snapshot = legacy;
            }
            bool found = false, incomplete = false;
            foreach (string key in new[] { "primary", "secondary" })
            {
                if (!snapshot.ContainsKey(key)) { incomplete = true; continue; }
                object raw = Json.Get(snapshot, key); if (raw == null) continue;
                var window = raw as Dictionary<string, object>; double used;
                if (window == null || !Number(Json.Get(window, "usedPercent"), out used) || used > 100) { incomplete = true; continue; }
                found = true;
                if (100 - used < 80) return true;
            }
            return found && !incomplete ? (bool?)false : null;
        }

        private static string Expiration(object value)
        {
            double seconds;
            if (!Number(value, out seconds) || seconds <= 0) return "";
            try { return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds).ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR")); }
            catch (ArgumentOutOfRangeException) { return ""; }
        }
    }

    // Persist before sending, so a retry after a lost response or app restart uses the same key.
    internal sealed class ResetRedemption
    {
        private readonly AppState state;
        private readonly Action persist;
        public ResetRedemption(AppState state, Action persist) { this.state = state; this.persist = persist; }
        public bool Pending { get { return !string.IsNullOrEmpty(state.PendingResetIdempotencyKey); } }

        public Task<string> Consume(CodexClient client, string email)
        {
            return Consume(key => client.RequestAsync("account/rateLimitResetCredit/consume", Json.Map("idempotencyKey", key)), email);
        }

        internal async Task<string> Consume(Func<string, Task<Dictionary<string, object>>> request, string email)
        {
            if (!Pending)
            {
                state.PendingResetIdempotencyKey = Guid.NewGuid().ToString();
                state.PendingResetAccountEmail = email;
                try { persist(); }
                catch { state.PendingResetIdempotencyKey = null; state.PendingResetAccountEmail = null; throw; }
            }
            string key = state.PendingResetIdempotencyKey, previousEmail = state.PendingResetAccountEmail;
            var response = await request(key);
            string outcome = Json.Text(response, "outcome");
            if (outcome == "reset" || outcome == "alreadyRedeemed" || outcome == "nothingToReset" || outcome == "noCredit")
            {
                state.PendingResetIdempotencyKey = null; state.PendingResetAccountEmail = null;
                try { persist(); }
                catch (Exception error)
                {
                    state.PendingResetIdempotencyKey = key; state.PendingResetAccountEmail = previousEmail;
                    throw new InvalidOperationException("O resultado chegou, mas não foi possível salvá-lo. Repita a conferência do mesmo pedido.", error);
                }
            }
            return outcome;
        }
    }
}
