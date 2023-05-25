using SGBackend.Entities;

namespace SGBackend.Controllers.Model;

public class ExportContainer
{
    public string adminToken { get; set; }
    public List<ExportUser> users { get; set; }
    public List<ExportMedium> media { get; set; }
}
