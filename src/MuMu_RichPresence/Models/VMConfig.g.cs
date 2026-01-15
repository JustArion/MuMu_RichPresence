#nullable enable
using System.Text.Json.Serialization;

namespace Dawn.MuMu.RichPresence.MuMu.Interop;

    public record Port(
        [property: JsonPropertyName("guest_ip")] string? GuestIP,
        [property: JsonPropertyName("host_port")] string HostPort
    );
    public record Nat(
        [property: JsonPropertyName("port_forward")] PortForward PortForward
    );

    public record PortForward(
        [property: JsonPropertyName("adb")] Port ADB,
        [property: JsonPropertyName("api")] Port API,
        [property: JsonPropertyName("event")] Port Event,
        [property: JsonPropertyName("frontend")] Port Frontend,
        [property: JsonPropertyName("gateway")] Port Gateway,
        [property: JsonPropertyName("input")] Port Input
    );

    public record VMConfig(
        [property: JsonPropertyName("vm")] Vm VM
    );

    public record Vm(
        [property: JsonPropertyName("nat")] Nat NAT
    );


