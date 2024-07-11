namespace Content.Server.Crazy;

[RegisterComponent]
public sealed partial class GptRespondentComponent : Component
{
    [DataField]
    public float Temperature = 0.003f;
}
