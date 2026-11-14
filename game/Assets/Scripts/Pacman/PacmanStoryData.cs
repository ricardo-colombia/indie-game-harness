namespace Studio.Pacman
{
    /// <summary>Which picture a prologue panel shows.</summary>
    public enum StoryScene { Dawn, Farmer, Snake, Taxes, Guerrilla, Boss, Finale }

    /// <summary>One panel of the comic that plays before the first level.</summary>
    public sealed class StoryPanel
    {
        public StoryScene Scene;
        public string Title;
        public string Text;
        public string Motive;
        public float Seconds;
    }

    /// <summary>
    /// The prologue's script. Plain data, no scene access: who is introduced, what they want. The tone is a farm
    /// fable: the farmer keeps his dignity, nobody is a real organisation, no weapon or violence is shown.
    /// </summary>
    public static class PacmanStoryData
    {
        public static readonly StoryPanel[] Panels =
        {
            new StoryPanel
            {
                Scene = StoryScene.Dawn, Seconds = 4.5f,
                Title = "LA FINCA",
                Text = "Cordillera central. Antes de que salga el sol, ya hay luz en la finca.",
                Motive = "",
            },
            new StoryPanel
            {
                Scene = StoryScene.Farmer, Seconds = 5f,
                Title = "DON ELISEO",
                Text = "Cuida estos cafetos desde antes de que su nieta naciera. Este año el grano maduró todo junto y las lluvias ya vienen.",
                Motive = "MISIÓN: recoger todos los granos antes del anochecer.",
            },
            new StoryPanel
            {
                Scene = StoryScene.Snake, Seconds = 4.5f,
                Title = "LA CORAL",
                Text = "Vive bajo las ramas y no le tiene rencor a nadie. Defiende su casa: quien la pisa recibe la mordida.",
                Motive = "QUIERE: que la dejen en paz.",
            },
            new StoryPanel
            {
                Scene = StoryScene.Taxes, Seconds = 4.5f,
                Title = "EL SEÑOR DE LOS IMPUESTOS",
                Text = "Todo lo cuenta y todo lo cobra. Quiere su parte antes de que el grano se seque.",
                Motive = "QUIERE: su parte, y la tuya.",
            },
            new StoryPanel
            {
                Scene = StoryScene.Guerrilla, Seconds = 4.5f,
                Title = "EL GUERRILLERO",
                Text = "Pasa por la vereda pidiendo colaboración para una guerra que Don Eliseo nunca pidió.",
                Motive = "QUIERE: la colaboración de todos.",
            },
            new StoryPanel
            {
                Scene = StoryScene.Boss, Seconds = 4.5f,
                Title = "EL PATRÓN",
                Text = "Llega en camioneta con un maletín. No quiere café: quiere la tierra para sembrar otra cosa.",
                Motive = "QUIERE: comprar la finca.",
            },
            new StoryPanel
            {
                Scene = StoryScene.Finale, Seconds = 5f,
                Title = "A COSECHAR",
                Text = "Solo hay una forma de salvar la cosecha: recogerla toda, antes de que se acerquen.",
                Motive = "",
            },
        };
    }

    /// <summary>
    /// Runs the prologue: which panel is showing and for how long. Panels advance by themselves, on a tap,
    /// or all at once when skipped. A freshly shown panel ignores taps for a moment so a double tap
    /// cannot swallow one.
    /// </summary>
    public sealed class PacmanStoryPlayer
    {
        public const float TapGuard = 0.3f;

        private readonly StoryPanel[] panels;

        public PacmanStoryPlayer(StoryPanel[] panels)
        {
            this.panels = panels;
        }

        public int Index { get; private set; }
        public float Time { get; private set; }
        public bool Done { get; private set; }
        public int Count => panels.Length;
        public StoryPanel Current => Done ? null : panels[Index];

        /// <summary>0..1 through the current panel.</summary>
        public float Progress
        {
            get
            {
                if (Done || panels[Index].Seconds <= 0f)
                {
                    return 1f;
                }
                float p = Time / panels[Index].Seconds;
                return p < 0f ? 0f : (p > 1f ? 1f : p);
            }
        }

        public void Tick(float dt)
        {
            if (Done)
            {
                return;
            }
            Time += dt;
            if (Time >= panels[Index].Seconds)
            {
                Next();
            }
        }

        /// <summary>A tap or Enter: go to the next panel, or finish after the last one.</summary>
        public void Advance()
        {
            if (!Done && Time >= TapGuard)
            {
                Next();
            }
        }

        public void Skip()
        {
            Done = true;
        }

        private void Next()
        {
            if (Index + 1 >= panels.Length)
            {
                Done = true;
                return;
            }
            Index++;
            Time = 0f;
        }
    }
}
