namespace OriCoopDedicatedServer.Core.API;

public abstract class ServerModule
{
	public string Name { get; set; } = "NONAME";


	public abstract void OnEnable();

	public void Info(string message)
	{
		Logger.Info(Name, message);
	}

	public void Warn(string message)
	{
		Logger.Warning(Name, message);
	}

	public void Error(string message)
	{
		Logger.Error(Name, message);
	}
}

