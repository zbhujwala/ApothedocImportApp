public class ConsoleSpinner
{
    public ConsoleSpinner()
    {
    }
    public static void StartLoadingIndicator()
    {
        try
        {
            var counter = 0;
            while (true)
            {
                counter++;
                switch (counter % 4)
                {
                    case 0: Console.Write("/"); break;
                    case 1: Console.Write("-"); break;
                    case 2: Console.Write("\\"); break;
                    case 3: Console.Write("|"); break;
                }
                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                Thread.Sleep(100);
            }

        }
        catch (ThreadInterruptedException)
        {
            ClearConsoleLine();
        }
        catch(ArgumentOutOfRangeException)
        {
            ClearConsoleLine() ;
        }
    }

    static void ClearConsoleLine()
    {
        int currentLineCursor = Console.CursorTop;
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write(new string(' ', Console.WindowWidth));
        Console.SetCursorPosition(0, currentLineCursor);
    }
}