namespace Apolice.Application.Features.Auth.LoginDemo;

// persona: corretora | corretor | analista | admin (PersonasDemo).
public sealed record LoginDemoRequest(string Persona);

public sealed record LoginDemoCommand(string Persona);
