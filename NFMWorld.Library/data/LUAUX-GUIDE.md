# LuauX Guide

When in doubt see documentation at https://github.com/luau-xml/luaux.

LuauX is basically like JSX for Lua, allowing you to write XML-like syntax directly in your Lua code.

## Example

```lua
local Sx = require("Sx")

local App = function()
	return (
		<Sx.View>
			<Sx.Text>Hello, {"LuauX"}!</Sx.Text>
		</Sx.View>
	)
end
```

Text inside `<Sx.Text>` tags does not need to be wrapped in curly braces. Text that reads signals inside of a {}
expression will automatically become reactive.