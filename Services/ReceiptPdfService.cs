using System.Globalization;
using System.Text;
using GiftOfTheGivers.Helpers.Models;
namespace GiftOfTheGivers.Services;

public static class ReceiptPdfService
{
    public static byte[] Generate(Donation donation)
    {
        static string Escape(string value) => new string(value.Select(c => c is >= ' ' and <= '~' ? c : '?').ToArray())
            .Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        var lines = new[]
        {
            "RELIEF SUPPORT PORTAL", "Prototype donation receipt", "",
            "Reference: " + donation.ReferenceNumber, "Donor: " + donation.DonorName,
            "Email: " + donation.Email,
            "Amount: " + donation.Currency + " " + donation.Amount.ToString("N2", CultureInfo.InvariantCulture),
            "Recorded: " + donation.DonationDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
            "Project: " + (donation.ReliefProject?.Name ?? "General relief fund"), "",
            "DEMO RECORD ONLY - NO PAYMENT PROCESSED", "This is not a valid tax certificate.",
            "Not affiliated with the real Gift of the Givers Foundation."
        };
        var content = new StringBuilder("BT /F1 12 Tf 50 790 Td 22 TL\n");
        foreach (var line in lines)
        {
            if (line.Length == 0) { content.Append("() Tj T*\n"); continue; }
            for (int start = 0; start < line.Length; start += 40)
                content.Append('(').Append(Escape(line.Substring(start, Math.Min(40, line.Length - start)))).Append(") Tj T*\n");
        }
        content.Append("ET\n");
        var stream = content.ToString();
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream"
        };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (int i = 0; i < objects.Length; i++) { offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString())); pdf.Append($"{i+1} 0 obj\n{objects[i]}\nendobj\n"); }
        var xref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append($"xref\n0 {objects.Length+1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) pdf.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Length+1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
