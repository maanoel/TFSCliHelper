using System;

namespace PEPCliHelper
{
  public class HelpBuilder : ICommandBuilder
  {

    public ICommandExecutor Executor { get; private set; }

    public HelpBuilder()
    {
    }

    public void Build(string arguments)
    {
      BreakLine();
      Console.Write(@"Exemplos dos comandos disponíveis - Para mais detalhes vide README");
      BreakLine();
      WriteBlue("   get all ");
      Console.Write("Realiza o get do Sau-PEP, Sau-Saude e FrameHTML em todas as versões. Ex: get all");
      BreakLine();
      WriteBlue("   get version X ");
      Console.Write("Realiza o get do Sau-PEP, Sau-Saude e FrameHTML na versão especificada. Ex: get version 2302");
      BreakLine();
      WriteBlue("   build all ");
      Console.Write("Realiza o build do Sau-PEP e Sau-Saude em todas as versões. Ex: build all");
      BreakLine();
      WriteBlue("   build version X ");
      Console.Write("Realiza o build do Sau-PEP e Sau-Saude na versão especificada. Ex: build version 2209");
      BreakLine();
      WriteBlue("   merge version X ");
      Console.Write("Realiza o merge de um changeset para todas as versões. Comando formado por 4 parâmetros. Ex: 'merge project version changeset' = merge back 32 81021 ");
      WriteBlue("   kill host ");
      Console.Write("Mata o rm.host.exe.");
      BreakLine();
      WriteBlue("   kill all ");
      Console.Write("Mata todas as instância iniciadas com RM. Inclui o RM.host.exe, RM.host.service e o RM.exe ");
      BreakLine();
      WriteBlue("   delete broker");
      Console.Write(" Remove o arquivo _broker na versão especificada Ex: delete broker 32");
      BreakLine();
      WriteBlue("   open host X ");
      Console.Write("Abre rm.host.exe da versão especificada. Ex: open host 2209");
      BreakLine();
      WriteBlue("   open rm X ");
      Console.Write("Abre rm.exe da versão especificada. Ex: open rm atual");
      BreakLine();
      WriteBlue("   cmd command ");
      Console.Write("Executa um comando no CMD utilizando o PEPCli Ex: cmd systeminfo");
      BreakLine();
      WriteBlue("   clear ");
      Console.Write("Limpa o PEPCli Ex: clear ou cls");
      BreakLine();
      BreakLine();
    }

    private void BreakLine()
    {
      Console.WriteLine("");
    }

    private void WriteBlue(string text) {

      Console.ForegroundColor = ConsoleColor.Blue;
      Console.Write(text);
      Console.ResetColor();
    }

  }
}