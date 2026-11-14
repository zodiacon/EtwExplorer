namespace EtwManifestParsing {
	public sealed class EtwKeyword {
		public string Name { get; set; } = string.Empty;
		public string? Message { get; set; }
		public ulong Mask { get; set; }

		public override string ToString() => $"{Name} {Message} 0x{Mask:X}";

	}
}
