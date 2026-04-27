using System.Globalization;
using System.Text;
using SaaS_BasePlatform.Application.DTOs.AccountsPayable;

namespace SaaS_BasePlatform.Application.Services.AccountsPayable
{
    public static class CsvWriter
    {
        private static readonly string[] Headers =
        {
            "Id",
            "Description",
            "Amount",
            "DueDate",
            "CategoryId",
            "CategoryName",
            "Status",
            "PaidAt",
            "PaymentMethod",
            "SupplierName",
            "Notes",
            "CancelledAt",
            "CancellationReason",
            "IsOverdue",
            "CreatedAt",
            "UpdatedAt"
        };

        public static byte[] WriteEntries(IEnumerable<EntryDto> entries)
        {
            var culture = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();

            sb.AppendLine(string.Join(',', Headers));

            foreach (var e in entries)
            {
                sb.Append(Escape(e.Id.ToString())).Append(',');
                sb.Append(Escape(e.Description)).Append(',');
                sb.Append(e.Amount.ToString("F2", culture)).Append(',');
                sb.Append(e.DueDate.ToString("yyyy-MM-dd", culture)).Append(',');
                sb.Append(Escape(e.CategoryId.ToString())).Append(',');
                sb.Append(Escape(e.CategoryName)).Append(',');
                sb.Append(Escape(e.Status.ToString())).Append(',');
                sb.Append(Escape(e.PaidAt?.ToString("yyyy-MM-dd HH:mm:ss", culture))).Append(',');
                sb.Append(Escape(e.PaymentMethod?.ToString())).Append(',');
                sb.Append(Escape(e.SupplierName)).Append(',');
                sb.Append(Escape(e.Notes)).Append(',');
                sb.Append(Escape(e.CancelledAt?.ToString("yyyy-MM-dd HH:mm:ss", culture))).Append(',');
                sb.Append(Escape(e.CancellationReason)).Append(',');
                sb.Append(e.IsOverdue ? "true" : "false").Append(',');
                sb.Append(e.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", culture)).Append(',');
                sb.Append(Escape(e.UpdatedAt?.ToString("yyyy-MM-dd HH:mm:ss", culture)));
                sb.AppendLine();
            }

            var preamble = Encoding.UTF8.GetPreamble();
            var body = Encoding.UTF8.GetBytes(sb.ToString());
            var output = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, output, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, output, preamble.Length, body.Length);
            return output;
        }

        private static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var needsQuoting = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
            var escaped = value.Replace("\"", "\"\"");
            return needsQuoting ? $"\"{escaped}\"" : escaped;
        }
    }
}
