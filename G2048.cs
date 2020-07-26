// #define USE_ASYNC

#if USE_ASYNC
using System.Collections.Concurrent;
using System.Threading.Tasks;
#endif
using System.Linq;
using System.Text;
using System;

namespace _2048
{
    public enum MoveDirection
        : ushort
    {
        Up = 'W',
        Down = 'S',
        Left = 'A',
        Right = 'D'
    }

    public sealed class G2048
    {
#if USE_ASYNC
        private readonly ConcurrentQueue<char> _keybuffer = new();
#endif
        private readonly (int Value, bool IsBlocked)[,] _board;
        private readonly Random _rand = new Random();
        private bool _isDone;
        private bool _isWon;
        private bool _isMoved;
        private int _score;

        public int Size { get; }


        public G2048(int size)
        {
            Size = size;
            _board = new (int, bool)[Size, Size];
            InitializeBoard();
        }

        private void InitializeBoard()
        {
            _isDone = false;
            _isWon = false;
            _isMoved = true;
            _score = 0;

            for (int y = 0; y < Size; ++y)
                for (int x = 0; x < Size; ++x)
                    _board[x, y] = default;
#if USE_ASYNC
            _keybuffer.Clear();

            Task.Factory.StartNew(async () =>
            {
                while (!_isDone)
                    if (Console.KeyAvailable)
                    {
                        ConsoleKeyInfo key = Console.ReadKey(true);

                        _keybuffer.Enqueue(key.KeyChar);
                    }
                    else
                        await Task.Delay(5);
            });
#endif
        }

#if USE_ASYNC
        public async Task Loop()
        {
            bool draw;
#else
        public void Loop()
        {
#endif
            AddTile();

            while (true)
            {
                if (_isMoved)
                    AddTile();
#if USE_ASYNC
                if (draw = !(_keybuffer.TryPeek(out char c) && Enum.IsDefined(typeof(MoveDirection), (MoveDirection)c)))
                    DrawBoard();
#else
                DrawBoard();
#endif
                if (_isDone)
                    break;
#if USE_ASYNC
                await WaitKey();
            }

            if (!draw)
                DrawBoard();
#else
                WaitKey();
            }
#endif
            Console.WriteLine(_isWon ? "You've made it!" : "Game Over!");
        }

        private void DrawBoard()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Score: " + _score + "\n");

            for (int y = 0; y < Size; ++y)
            {
                Console.Write($"{(y == 0 ? '┌' : '├')}{string.Join(y == 0 ? "┬" : "┼", Enumerable.Repeat("────", Size))}{(y == 0 ? '┐' : '┤')}\n│");

                for (int x = 0; x < Size; ++x)
                {
                    (int val , _) = _board[x, y];

                    Console.ForegroundColor = val switch
                    {
                        < 4 => ConsoleColor.White,
                        4 => ConsoleColor.Yellow,
                        8 => ConsoleColor.DarkYellow,
                        16 => ConsoleColor.Red,
                        32 => ConsoleColor.DarkRed,
                        64 => ConsoleColor.Magenta,
                        128 => ConsoleColor.Blue,
                        256 => ConsoleColor.DarkCyan,
                        512 => ConsoleColor.Cyan,
                        1024 => ConsoleColor.Green,
                        2048 => ConsoleColor.DarkGreen,
                        _ => ConsoleColor.DarkBlue,
                    };
                    Console.Write((val == 0 ? "" : val.ToString()).PadLeft(4));
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.Write("│");
                }

                Console.WriteLine();
            }

            Console.WriteLine($"└{string.Join("┴", Enumerable.Repeat("────", Size))}┘\n");
            Console.WriteLine(string.Join("   ", Enum.GetValues(typeof(MoveDirection)).Cast<MoveDirection>().Select(v => $"[{(char)v}] {v}")));
        }

#if USE_ASYNC
        private async Task WaitKey()
        {
            _isMoved = false;

            do
                if (_keybuffer.TryDequeue(out char @char) && Move((MoveDirection)char.ToUpper(@char)))
                    break;
                else
                    await Task.Delay(10);
            while (true);
#else
        private void WaitKey()
        {
            _isMoved = false;

            while (!Move((MoveDirection)char.ToUpper(Console.ReadKey(true).KeyChar)))
                ;
#endif
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    _board[x, y].IsBlocked = false;
        }

        private void AddTile()
        {
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    if (_board[x, y].Value != 0)
                        continue;

                    int a, b;

                    do
                    {
                        a = _rand.Next(0, Size);
                        b = _rand.Next(0, Size);
                    } while (_board[a, b].Value != 0);

                    double r = _rand.NextDouble();

                    _board[a, b].Value = r > 0.89f ? 4 : 2;

                    if (CanMove())
                        return;
                }

            _isDone = true;
        }

        private bool CanMove()
        {
            for (int y = 0; y < Size; ++y)
                for (int x = 0; x < Size; ++x)
                    if (_board[x, y].Value == 0)
                        return true;

            for (int y = 0; y < Size; ++y)
                for (int x = 0; x < Size; ++x)
                    if (TestAdd(x + 1, y, _board[x, y].Value) ||
                        TestAdd(x - 1, y, _board[x, y].Value) ||
                        TestAdd(x, y + 1, _board[x, y].Value) ||
                        TestAdd(x, y - 1, _board[x, y].Value))
                        return true;

            return false;
        }

        private bool TestAdd(int x, int y, int value) => x >= 0 && x < Size && y >= 0 && y < Size && _board[x, y].Value == value;

        private void MoveVertically(int x, int y, int d)
        {
            if (_board[x, y + d].Value != 0 &&
                _board[x, y + d].Value == _board[x, y].Value &&
                !_board[x, y].IsBlocked &&
                !_board[x, y + d].IsBlocked)
            {
                _board[x, y].Value = 0;
                _board[x, y + d].Value <<= 1;
                _score += _board[x, y + d].Value;
                _board[x, y + d].IsBlocked = true;
                _isMoved = true;
            }
            else if (_board[x, y + d].Value == 0 && _board[x, y].Value != 0)
            {
                _board[x, y + d].Value = _board[x, y].Value;
                _board[x, y].Value = 0;
                _isMoved = true;
            }

            if (d > 0)
            {
                if (y + d < Size - 1)
                    MoveVertically(x, y + d, 1);
            }
            else if (y + d > 0)
                MoveVertically(x, y + d, -1);
        }

        private void MoveHorizontally(int x, int y, int d)
        {
            if (_board[x + d, y].Value != 0 &&
                _board[x + d, y].Value == _board[x, y].Value &&
                !_board[x + d, y].IsBlocked &&
                !_board[x, y].IsBlocked)
            {
                _board[x, y].Value = 0;
                _board[x + d, y].Value <<= 1;
                _score += _board[x + d, y].Value;
                _board[x + d, y].IsBlocked = true;
                _isMoved = true;
            }
            else if (_board[x + d, y].Value == 0 && _board[x, y].Value != 0)
            {
                _board[x + d, y].Value = _board[x, y].Value;
                _board[x, y].Value = 0;
                _isMoved = true;
            }

            if (d > 0)
            {
                if (x + d < Size - 1)
                    MoveHorizontally(x + d, y, 1);
            }
            else if (x + d > 0)
                MoveHorizontally(x + d, y, -1);
        }

        private bool Move(MoveDirection direction)
        {
            switch (direction)
            {
                case MoveDirection.Up:
                    for (int x = 0; x < Size; ++x)
                    {
                        int y = 1;

                        while (y < Size)
                        {
                            if (_board[x, y].Value != 0)
                                MoveVertically(x, y, -1);

                            ++y;
                        }
                    }

                    return true;
                case MoveDirection.Down:
                    for (int x = 0; x < Size; ++x)
                    {
                        int y = Size - 2;

                        while (y >= 0)
                        {
                            if (_board[x, y].Value != 0)
                                MoveVertically(x, y, 1);

                            --y;
                        }
                    }

                    return true;
                case MoveDirection.Left:
                    for (int y = 0; y < Size; ++y)
                    {
                        int x = 1;

                        while (x < Size)
                        {
                            if (_board[x, y].Value != 0)
                                MoveHorizontally(x, y, -1);

                            ++x;
                        }
                    }

                    return true;
                case MoveDirection.Right:
                    for (int y = 0; y < Size; ++y)
                    {
                        int x = Size - 2;

                        while (x >= 0)
                        {
                            if (_board[x, y].Value != 0)
                                MoveHorizontally(x, y, 1);

                            --x;
                        }
                    }

                    return true;
            }

            return false;
        }

#if USE_ASYNC
        public static async Task Main(string[] args)
#else
        public static void Main(string[] args)
#endif
        {
            if (!int.TryParse(args.FirstOrDefault(), out int size))
                size = 4;

            G2048 game = new G2048(size);

            Console.OutputEncoding = Encoding.Unicode;

            do
            {
                game.InitializeBoard();
#if USE_ASYNC
                await game.Loop();
#else
                game.Loop();
#endif
                Console.WriteLine("[N] New game   [P] Exit");
            input:
                switch (char.ToUpper(Console.ReadKey(true).KeyChar))
                {
                    case 'N':
                        continue;
                    case 'P':
                        return;
                    default:
                        goto input;
                }
            }
            while (true);
        }
    }
}