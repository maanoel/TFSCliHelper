﻿namespace PEPCliHelper
{
  public class ComandLineInterface : ICommandLineInterface
  {
    private string _arg = "";

    public void WelcomeMessage()
    {
      System.Console.WriteLine("Welcome");
    }

    public ICommandLineInterface WaitCommand()
    {
      System.Console.WriteLine("Please type the command you want to execute...");
      System.Console.WriteLine("You can type help to see avalible commands.");

      _arg = System.Console.ReadLine();
      return this;
    }

    public ICommandLineInterface Run()
    {
      try
      {
        ICommandBuilder command = CommandFactory.Create(_arg);
        command.Build(_arg);
      }
      catch (InvalidCommandException ex)
      {
        System.Console.WriteLine(ex.Message);
      }

      return this;
    }
  }
}