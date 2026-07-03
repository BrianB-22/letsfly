using CheckRide.Models;

namespace CheckRide;

public class TrayApp : ApplicationContext
{
    private readonly FlightListForm _form;

    public TrayApp(SupabaseSession session)
    {
        var client = new SupabaseClient(session);
        _form = new FlightListForm(client);
        _form.FormClosed += (s, e) => ExitThread();
        _form.Show();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _form.Dispose();
        base.Dispose(disposing);
    }
}
