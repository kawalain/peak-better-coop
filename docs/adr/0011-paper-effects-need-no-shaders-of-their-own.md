# Paper effects need no shaders of their own

The paper's effects that vary across a surface, such as stamp ink drying back
in patches of noise or a photo developing darks first, are drawn with the
game's own UI material: a UI mesh fine enough that its vertex colours carry the
pattern, as the pen strokes already are, over drawings and photos made as
before. The mod ships no shader and no asset bundle.

## Considered Options

- A shader of the mod's own, built into an asset bundle: per-pixel noise and
  crisper edges, but a Unity build step outside `dotnet build`, a bundle tied
  to the game's Unity version and graphics APIs, and one more thing to break
  when the game updates.
- The game's "Scouts/UI" window material, whose dither noise already cuts the
  paper's faded edges: free, but its noise is fine grain that jumps a few
  times a second, so ink would fizz away rather than dry.

## Consequences

- A pattern is only as fine as the mesh under it, so its edges are soft by
  nature; a drawing that needs a crisp shape keeps it in its SVG.
- Paper still takes its grain from the game's material (`PaperSheet`), so the
  game's look and the mod's effects stay together.
