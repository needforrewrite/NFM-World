---@class BaseMouseDragEvent : System.IEquatable_BaseMouseDragEvent
---@field dragStart LuaVector2
---@field position LuaVector2
---@field button integer
---@field buttons MouseButtons
---@field ctrlKey boolean
---@field metaKey boolean
---@field shiftKey boolean

BaseMouseDragEvent = {}


---@class BaseMouseEvent : System.IEquatable_BaseMouseEvent
---@field position LuaVector2
---@field button MouseButton
---@field buttons MouseButtons
---@field ctrlKey boolean
---@field altKey boolean
---@field shiftKey boolean

BaseMouseEvent = {}


---@class BaseMouseMoveEvent : System.IEquatable_BaseMouseMoveEvent
---@field position LuaVector2
---@field buttons MouseButtons
---@field ctrlKey boolean
---@field altKey boolean
---@field shiftKey boolean

BaseMouseMoveEvent = {}


---@class BaseMouseWheelEvent : System.IEquatable_BaseMouseWheelEvent
---@field delta LuaVector3
---@field position LuaVector2
---@field buttons MouseButtons
---@field ctrlKey boolean
---@field metaKey boolean
---@field shiftKey boolean

BaseMouseWheelEvent = {}


---@class KeyboardEvent : System.IEquatable_KeyboardEvent
---@field keyChar Key
---@field keyCode Key
---@field keys Keys

KeyboardEvent = {}


---@class KeyboardTypingEvent : System.IEquatable_KeyboardTypingEvent
---@field keyChar integer

KeyboardTypingEvent = {}


---@class MouseDragEvent : System.IEquatable_MouseDragEvent
---@field dragStart LuaVector2
---@field relativeDragStart LuaVector2
---@field position LuaVector2
---@field button integer
---@field buttons MouseButtons
---@field ctrlKey boolean
---@field metaKey boolean
---@field shiftKey boolean
---@field relativePosition LuaVector2

MouseDragEvent = {}


---@class MouseEvent : System.IEquatable_MouseEvent
---@field position LuaVector2
---@field button MouseButton
---@field buttons MouseButtons
---@field ctrlKey boolean
---@field metaKey boolean
---@field shiftKey boolean
---@field relativePosition LuaVector2

MouseEvent = {}


---@class MouseMoveEvent : System.IEquatable_MouseMoveEvent
---@field position LuaVector2
---@field buttons MouseButtons
---@field ctrlKey boolean
---@field metaKey boolean
---@field shiftKey boolean
---@field relativePosition LuaVector2

MouseMoveEvent = {}


---@class MouseWheelEvent : System.IEquatable_MouseWheelEvent
---@field delta LuaVector3
---@field position LuaVector2
---@field buttons MouseButtons
---@field ctrlKey boolean
---@field metaKey boolean
---@field shiftKey boolean
---@field relativePosition LuaVector2

MouseWheelEvent = {}


---@class Component : Node, NFMWorld.Reactor.IAnimationCallback
---@field visualChildren { Node }
---@field canHaveChildren boolean
---@field name string
---@field isFocusable boolean
---@field layoutMarginPosition LuaVector2
---@field layoutMarginSize LuaVector2
---@field layoutBorderPosition LuaVector2
---@field layoutBorderSize LuaVector2
---@field layoutPaddingPosition LuaVector2
---@field layoutPaddingSize LuaVector2
---@field layoutContentPosition LuaVector2
---@field layoutContentSize LuaVector2
---@field layoutMargin LuaVector2
---@field layoutPadding LuaVector2
---@field layoutBorder LuaVector2
---@field scissorRect LuaRect
---@field layoutWidth number
---@field layoutHeight number
---@field layoutX number
---@field layoutY number
---@field layoutDirection Direction
---@field hadOverflow boolean
---@field layoutMarginTop number
---@field layoutMarginBottom number
---@field layoutMarginLeft number
---@field layoutMarginRight number
---@field layoutPaddingTop number
---@field layoutPaddingBottom number
---@field layoutPaddingLeft number
---@field layoutPaddingRight number
---@field layoutBorderTop number
---@field layoutBorderBottom number
---@field layoutBorderLeft number
---@field layoutBorderRight number
---@field hasNewLayout boolean
---@field isDirty boolean
---@field isReferenceBaseline boolean
---@field scrollLeft number
---@field scrollTop number
---@field scrollableWidth number
---@field scrollableHeight number
---@field isClipping boolean
---@field isDisplayed boolean
---@field visualParent Node|nil
---@field addChild fun(self: Component, child: Node)
---@field insertAt fun(self: Component, index: integer, child: Node)
---@field removeAt fun(self: Component, index: integer)
---@field scrollIntoView fun(self: Component)
---@field focus fun(self: Component)
---@field blur fun(self: Component)

Component = {}


---@class Direction : System.Enum, System.IComparable, System.IConvertible, System.ISpanFormattable, System.IFormattable

Direction = {}


---@class Node
---@field visualParent Node|nil
---@field visualChildren { Node }

Node = {}


---@class TextInput : Component, NFMWorld.Reactor.IAnimationCallback
---@field placeholder string
---@field text string
---@field visualChildren { Node }
---@field canHaveChildren boolean
---@field name string
---@field isFocusable boolean
---@field layoutMarginPosition LuaVector2
---@field layoutMarginSize LuaVector2
---@field layoutBorderPosition LuaVector2
---@field layoutBorderSize LuaVector2
---@field layoutPaddingPosition LuaVector2
---@field layoutPaddingSize LuaVector2
---@field layoutContentPosition LuaVector2
---@field layoutContentSize LuaVector2
---@field layoutMargin LuaVector2
---@field layoutPadding LuaVector2
---@field layoutBorder LuaVector2
---@field scissorRect LuaRect
---@field layoutWidth number
---@field layoutHeight number
---@field layoutX number
---@field layoutY number
---@field layoutDirection Direction
---@field hadOverflow boolean
---@field layoutMarginTop number
---@field layoutMarginBottom number
---@field layoutMarginLeft number
---@field layoutMarginRight number
---@field layoutPaddingTop number
---@field layoutPaddingBottom number
---@field layoutPaddingLeft number
---@field layoutPaddingRight number
---@field layoutBorderTop number
---@field layoutBorderBottom number
---@field layoutBorderLeft number
---@field layoutBorderRight number
---@field hasNewLayout boolean
---@field isDirty boolean
---@field isReferenceBaseline boolean
---@field scrollLeft number
---@field scrollTop number
---@field scrollableWidth number
---@field scrollableHeight number
---@field isClipping boolean
---@field isDisplayed boolean
---@field visualParent Node|nil
---@field addChild fun(self: TextInput, child: Node)
---@field insertAt fun(self: TextInput, index: integer, child: Node)
---@field removeAt fun(self: TextInput, index: integer)
---@field scrollIntoView fun(self: TextInput)
---@field focus fun(self: TextInput)
---@field blur fun(self: TextInput)

TextInput = {}


---@class TextNode : Node, NFMWorld.Reactor.IReceivesTextInvalidation, NFMWorld.Reactor.IRichTextLeaf, NFMWorld.Reactor.IRichTextElement
---@field visualChildren { Node }
---@field text string
---@field visualParent Node|nil

TextNode = {}


---@class View : Component, NFMWorld.Reactor.IAnimationCallback
---@field visualChildren { Node }
---@field canHaveChildren boolean
---@field name string
---@field isFocusable boolean
---@field layoutMarginPosition LuaVector2
---@field layoutMarginSize LuaVector2
---@field layoutBorderPosition LuaVector2
---@field layoutBorderSize LuaVector2
---@field layoutPaddingPosition LuaVector2
---@field layoutPaddingSize LuaVector2
---@field layoutContentPosition LuaVector2
---@field layoutContentSize LuaVector2
---@field layoutMargin LuaVector2
---@field layoutPadding LuaVector2
---@field layoutBorder LuaVector2
---@field scissorRect LuaRect
---@field layoutWidth number
---@field layoutHeight number
---@field layoutX number
---@field layoutY number
---@field layoutDirection Direction
---@field hadOverflow boolean
---@field layoutMarginTop number
---@field layoutMarginBottom number
---@field layoutMarginLeft number
---@field layoutMarginRight number
---@field layoutPaddingTop number
---@field layoutPaddingBottom number
---@field layoutPaddingLeft number
---@field layoutPaddingRight number
---@field layoutBorderTop number
---@field layoutBorderBottom number
---@field layoutBorderLeft number
---@field layoutBorderRight number
---@field hasNewLayout boolean
---@field isDirty boolean
---@field isReferenceBaseline boolean
---@field scrollLeft number
---@field scrollTop number
---@field scrollableWidth number
---@field scrollableHeight number
---@field isClipping boolean
---@field isDisplayed boolean
---@field visualParent Node|nil
---@field addChild fun(self: View, child: Node)
---@field insertAt fun(self: View, index: integer, child: Node)
---@field removeAt fun(self: View, index: integer)
---@field scrollIntoView fun(self: View)
---@field focus fun(self: View)
---@field blur fun(self: View)

View = {}


