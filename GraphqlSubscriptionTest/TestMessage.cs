public class TestMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? Message { get; set; }
}

[ExtendObjectType(typeof(TestMessage))]
public class TestMessageType { }