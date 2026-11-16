namespace BlueTube.Core;

/// <param name="Topic">Group shown in the UI.</param>
/// <param name="Handle">YouTube @handle. Used only when <paramref name="Id"/> is null.</param>
/// <param name="Name">Expected channel title. Add is refused if YouTube returns another name (impostor check).</param>
/// <param name="Tags">Comma-separated channel tags (feed chips).</param>
/// <param name="Why">One line: why this channel is in the library.</param>
/// <param name="Id">Verified "UC..." ID. Set after a verified add. Then add needs no API call.</param>
public sealed record LibraryChannel(string Topic, string Handle, string Name, string Tags, string Why, string? Id = null);

/// <summary>
/// The owner's curated learning library. Rules for entries:
/// deep, accurate teaching or substantive news; proven builders for business (no get-rich-quick);
/// English; matches the owner's goals (see docs/AGENT-GUIDE.md, "Learning library").
/// </summary>
public static class LearningLibrary
{
    public const string AiMl = "AI & ML engineering";
    public const string FullStack = "Full stack & system design";
    public const string Robotics = "Robotics & electronics";
    public const string GameDev = "Game development";
    public const string Business = "Business (proven builders)";
    public const string Cpp = "C++, embedded & CS fundamentals";
    public const string News = "Tech news";

    public static readonly IReadOnlyList<string> Topics = [AiMl, FullStack, Robotics, GameDev, Business, Cpp, News];

    public static readonly IReadOnlyList<LibraryChannel> Channels =
    [
        // AI & ML engineering: how it works + building AI products + local models + vision.
        new(AiMl, "@AndrejKarpathy", "Andrej Karpathy", "ai-ml", "Neural nets and LLMs from scratch. The best deep ML explanations.", "UCXUPKJO5MZQN11PqgIvyuvQ"),
        new(AiMl, "@3blue1brown", "3Blue1Brown", "ai-ml,math", "Visual deep dives: neural networks, transformers, linear algebra.", "UCYO_jab_esuFRV4b17AJtAw"),
        new(AiMl, "@statquest", "StatQuest with Josh Starmer", "ai-ml,math", "ML and statistics concepts, one clear step at a time.", "UCtYLUTtgS3k1Fg4y5tAhLbw"),
        new(AiMl, "@umarjamilai", "Umar Jamil", "ai-ml", "Transformers, diffusion and LLM internals coded line by line.", "UCtAcpQcYerN8xxZJYTfWBMw"),
        new(AiMl, "@WelchLabs", "Welch Labs", "ai-ml,math", "Beautiful deep explainers on how AI models really work.", "UConVfxXodg78Tzh5nNu85Ew"),
        new(AiMl, "@YannicKilcher", "Yannic Kilcher", "ai-ml", "AI research papers explained in depth.", "UCZHmQk67mSJgfCCTn7xBfew"),
        new(AiMl, "@sentdex", "sentdex", "ai-ml,python", "Python ML in practice, local models, AI hardware.", "UCfzlCWGWYyIQ0aLC5w48gBQ"),
        new(AiMl, "@technovangelist", "Matt Williams", "ai-ml,local-ai", "Running local models with Ollama; practical local AI.", "UCHaF9kM2wn8C3CLRwLkC2GQ"),
        new(AiMl, "@samwitteveenai", "Sam Witteveen", "ai-ml,agents", "Building LLM apps and agents; new models tested hands-on.", "UC55ODQSvARtgSyc8ThfiepQ"),
        new(AiMl, "@daveebbelaar", "Dave Ebbelaar", "ai-ml,agents,python", "AI engineering for real products: agents, RAG, structure.", "UCn8ujwUInbJkBhffxqAPBVQ"),
        new(AiMl, "@Roboflow", "Roboflow", "computer-vision,ai-ml", "Computer vision: object detection, training, deployment.", "UCUlRrGpNRT5jbiV8h5Q_4Fg"),
        new(AiMl, "@NicolaiAI", "Nicolai Nielsen", "computer-vision,robotics", "Computer vision and OpenCV for robots and cameras.", "UCpABUkWm8xMt5XmGcFb3EFg"),

        // Full stack & system design: .NET backend, Python backend, architecture.
        new(FullStack, "@ByteByteGo", "ByteByteGo", "system-design", "System design concepts in short, clear animations.", "UCZgt6AzoyjslHTC9dz0UoTw"),
        new(FullStack, "@hnasr", "Hussein Nasser", "backend,system-design", "Backend engineering deep dives: databases, protocols, proxies.", "UC_ML5xP23TOWKUcc-oAE_Eg"),
        new(FullStack, "@AsliEngineering", "Arpit Bhayani", "system-design,backend", "How real systems are built at scale, in depth.", "UC_b1GUJv_2QiMP4BxC9-Dxg"),
        new(FullStack, "@CodeOpinion", "CodeOpinion", "architecture,dotnet", "Software architecture and design trade-offs.", "UC3RKA4vunFAfrfxiJhPEplw"),
        new(FullStack, "@MilanJovanovicTech", "Milan Jovanović", "dotnet,backend,architecture", "Clean ASP.NET Core architecture, EF Core, real patterns.", "UCC_dVe-RI-vgCZfls06mDZQ"),
        new(FullStack, "@nickchapsas", "Nick Chapsas", "csharp,dotnet", "Modern C# and .NET features and performance.", "UCrkPsvLGln62OMZRO6K-llg"),
        new(FullStack, "@RawCoding", "Raw Coding", "dotnet,backend", "ASP.NET Core internals, auth and backend in depth.", "UCP_jWxjn__YXmo4iU7Low0g"),
        new(FullStack, "@ArjanCodes", "ArjanCodes", "python,architecture", "Python software design, FastAPI, clean code.", "UCVhQ2NnY5Rskt6UjCUkJ_DA"),

        // Robotics & electronics: hobby builds, control + autonomy, drones + hardware products.
        new(Robotics, "@jamesbruton", "James Bruton", "robotics", "Real robot builds: legged, balancing, robot arms.", "UCUbDcUPed50Y_7KmfCXKohA"),
        new(Robotics, "@greatscottlab", "GreatScott!", "electronics", "Electronics basics and DIY builds, well explained.", "UC6mIxFTvXkWQVEHPsEdflzQ"),
        new(Robotics, "@Dronebotworkshop", "DroneBot Workshop", "electronics,embedded,robotics", "Arduino, ESP32, Raspberry Pi, sensors and motors, step by step.", "UCzml9bXoEM0itbcE96CB03w"),
        new(Robotics, "@AndreasSpiess", "Andreas Spiess", "electronics,embedded", "ESP32, IoT and sensor tests by an engineer.", "UCu7_D0o48KbfhpEohoP7YSQ"),
        new(Robotics, "@BenEater", "Ben Eater", "electronics,cs-fundamentals", "Build a computer from chips: how hardware really works.", "UCS0N5baNlQWJCUrhCEo8WlA"),
        new(Robotics, "@PhilsLab", "Phil's Lab", "electronics,embedded,hardware", "PCB and embedded hardware design for real products.", "UCVryWqJ4cSlbTSETBHpBUWw"),
        new(Robotics, "@Eigensteve", "Steve Brunton", "control,robotics,math", "Control theory (PID, LQR), dynamics and data-driven engineering.", "UCm5mt-A4w61lknZ9lCsZtBw"),
        new(Robotics, "@ArticulatedRobotics", "Articulated Robotics", "robotics,ros2", "ROS 2 from zero to a working mobile robot.", "UCx9vSJTSZGFrErfPtut5GNQ"),
        new(Robotics, "@JoshuaBardwell", "Joshua Bardwell", "drones", "FPV drones: building, tuning, electronics.", "UCX3eufnI7A2I7IkKHZn8KSQ"),
        new(Robotics, "@TomStantonEngineering", "Tom Stanton", "drones,robotics", "Engineering builds: drones, aircraft, control experiments.", "UC67gfx2Fg7K2NSHqoENVgwA"),
        new(Robotics, "@StuffMadeHere", "Stuff Made Here", "robotics,hardware", "Hard engineering projects from idea to working machine.", "UCj1VqrHhDte54oLgPG4xpuQ"),

        // Game development: Unity + C#, game design, game business.
        new(GameDev, "@CodeMonkeyUnity", "Code Monkey", "unity,csharp", "Unity and C# tutorials, systems and tools.", "UCFK6NCbuCIVzA6Yj1G_ZqCg"),
        new(GameDev, "@git-amend", "git-amend", "unity,csharp,architecture", "Advanced Unity C# architecture and patterns (intermediate+).", "UCMAU6mx40NWsQuYmxAzPcHA"),
        new(GameDev, "@Unity3dCollege", "Jason Weimann", "unity,csharp", "Unity architecture and career advice from a long-time pro.", "UCX_b3NNQN5bzExm-22-NVVg"),
        new(GameDev, "@Tarodev", "Tarodev", "unity,csharp", "Unity techniques and game feel, short and practical.", "UCAg2py6olXgod71Ix06HnUQ"),
        new(GameDev, "@SebastianLague", "Sebastian Lague", "gamedev,graphics", "Coding adventures: simulations, graphics, physics.", "UCmtyQOKKmrMVaKuRXz02jbQ"),
        new(GameDev, "@GMTK", "Game Maker's Toolkit", "game-design", "Game design analysis; also his own game dev series.", "UCqJ-Xo29CKyLTjn6z2XwYAw"),
        new(GameDev, "@DesignDoc", "Design Doc", "game-design", "Why game mechanics and systems work.", "UCNOVwMpD-5A1xzcQGbIHNeA"),
        new(GameDev, "@JonasTyroller", "Jonas Tyroller", "gamedev,game-business", "Indie dev with a hit game: design and business with real numbers.", "UC_p_9arduPuxM8DHTGIuSOg"),
        new(GameDev, "@HowToMarketAGame", "How To Market A Game", "game-business", "Chris Zukowski: Steam marketing and wishlists, based on data.", "UCkPTbfinNHrbgAOfmfdf51Q"),
        new(GameDev, "@GDCFestivalofGaming", "GDC Festival of Gaming", "gamedev,game-design", "Game Developers Conference talks and postmortems.", "UC0JB7TSe49lg56u6qH8y_MQ"),

        // Business: proven builders only. Real products, real numbers.
        new(Business, "@ycombinator", "Y Combinator", "business,startups", "Startup advice from YC partners and founders.", "UCcefcZRL2oaA_uBNeo5UOWg"),
        new(Business, "@LennysPodcast", "Lenny's Podcast", "business,product", "Product and growth from people who built big products.", "UC6t1O76G0jYXOAoYCm153dA"),
        new(Business, "@starterstory", "Starter Story", "business,indie", "Founders show how their businesses make money.", "UChhw6DlKKTQ9mYSpTfXUYqA"),
        new(Business, "@marc-lou", "Marc Lou", "business,indie", "Solo maker shipping small profitable software products.", "UC12JO2IPlEViR-o6SLXKx1A"),

        // C++, embedded & CS fundamentals.
        new(Cpp, "@TheCherno", "The Cherno", "cpp,gamedev", "C++ series and game engine development.", "UCQ-W1KE9EYfdxhL6S4twUNw"),
        new(Cpp, "@CppCon", "CppCon", "cpp", "C++ conference talks by experts.", "UCMlGfpWw-RUdWX_JbLCukXg"),
        new(Cpp, "@JacobSorber", "Jacob Sorber", "c,embedded,cs-fundamentals", "C and systems programming, clear and practical.", "UCwd5VFu4KoJNjkWJZMFJGHQ"),
        new(Cpp, "@LowByteProductions", "Low Byte Productions", "embedded,c", "Embedded and low-level programming in depth.", "UC56l7uZA209tlPTVOJiJ8Tw"),
        new(Cpp, "@MollyRocket", "Molly Rocket", "performance,cpp", "Casey Muratori: performance-aware programming.", "UCaTznQhurW5AaiYPbhEA-KA"),
        new(Cpp, "@CoreDumpped", "Core Dumped", "cs-fundamentals", "Operating systems and how CPUs run code, animated.", "UCGKEMK3s-ZPbjVOIuAV8clQ"),
        new(Cpp, "@abdul_bari", "Abdul Bari", "cs-fundamentals,algorithms", "Algorithms and data structures, very clear.", "UCZCFT11CWBi3MHNlGf019nw"),
        new(Cpp, "@Computerphile", "Computerphile", "cs-fundamentals", "Computer science topics from university researchers.", "UC9-y-6csu5WGm29I7JiwpnA"),

        // Tech news with substance.
        new(News, "@Fireship", "Fireship", "news,programming", "Fast tech news and concepts in 100 seconds.", "UCsBjURrPoezykLs9EqgamOA"),
        new(News, "@aiexplained-official", "AI Explained", "news,ai-ml", "AI news analysed carefully, not hyped.", "UCNJ1Ymd5yFuUPtn21xtRbbw"),
    ];

    /// <summary>Channels the owner had before the library that no longer fit the goals.</summary>
    public static readonly IReadOnlyList<(string Name, string Reason)> Dropped =
    [
        ("Gamers Nexus", "Consumer PC hardware reviews and industry news. Not on the learning path."),
        ("Two Minute Papers", "Short and hype-heavy AI news. AI Explained and Yannic Kilcher cover it in more depth."),
    ];

    /// <summary>True if the title YouTube returns plausibly belongs to the expected channel.</summary>
    public static bool NameMatches(string expected, string actual)
    {
        var a = Normalize(expected);
        var b = Normalize(actual);
        return a.Length > 0 && b.Length > 0 && (a.Contains(b) || b.Contains(a));
    }

    private static string Normalize(string s) =>
        new(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
