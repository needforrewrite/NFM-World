declare extern type Node with
    visualParent: Node|nil
    visualChildren: { Node }
end

declare Node: {
}

declare extern type Component extends Node with
    visualChildren: { Node }
    canHaveChildren: boolean
    name: string
    isFocusable: boolean
    layoutMarginPosition: LuaVector2
    layoutMarginSize: LuaVector2
    layoutBorderPosition: LuaVector2
    layoutBorderSize: LuaVector2
    layoutPaddingPosition: LuaVector2
    layoutPaddingSize: LuaVector2
    layoutContentPosition: LuaVector2
    layoutContentSize: LuaVector2
    layoutMargin: LuaVector2
    layoutPadding: LuaVector2
    layoutBorder: LuaVector2
    scissorRect: LuaRect
    layoutWidth: number
    layoutHeight: number
    layoutX: number
    layoutY: number
    layoutDirection: Direction
    hadOverflow: boolean
    layoutMarginTop: number
    layoutMarginBottom: number
    layoutMarginLeft: number
    layoutMarginRight: number
    layoutPaddingTop: number
    layoutPaddingBottom: number
    layoutPaddingLeft: number
    layoutPaddingRight: number
    layoutBorderTop: number
    layoutBorderBottom: number
    layoutBorderLeft: number
    layoutBorderRight: number
    hasNewLayout: boolean
    isDirty: boolean
    isReferenceBaseline: boolean
    scrollLeft: number
    scrollTop: number
    scrollableWidth: number
    scrollableHeight: number
    isClipping: boolean
    isDisplayed: boolean
    visualParent: Node|nil
    addChild: ((self: Component, child: Node) -> nil)
    insertAt: ((self: Component, index: integer, child: Node) -> nil)
    removeAt: ((self: Component, index: integer) -> nil)
    scrollIntoView: ((self: Component) -> nil)
    focus: ((self: Component) -> nil)
    blur: ((self: Component) -> nil)
end

declare Component: {
}

declare extern type TextInput extends Component with
    placeholder: string
    text: string
    visualChildren: { Node }
    canHaveChildren: boolean
    name: string
    isFocusable: boolean
    layoutMarginPosition: LuaVector2
    layoutMarginSize: LuaVector2
    layoutBorderPosition: LuaVector2
    layoutBorderSize: LuaVector2
    layoutPaddingPosition: LuaVector2
    layoutPaddingSize: LuaVector2
    layoutContentPosition: LuaVector2
    layoutContentSize: LuaVector2
    layoutMargin: LuaVector2
    layoutPadding: LuaVector2
    layoutBorder: LuaVector2
    scissorRect: LuaRect
    layoutWidth: number
    layoutHeight: number
    layoutX: number
    layoutY: number
    layoutDirection: Direction
    hadOverflow: boolean
    layoutMarginTop: number
    layoutMarginBottom: number
    layoutMarginLeft: number
    layoutMarginRight: number
    layoutPaddingTop: number
    layoutPaddingBottom: number
    layoutPaddingLeft: number
    layoutPaddingRight: number
    layoutBorderTop: number
    layoutBorderBottom: number
    layoutBorderLeft: number
    layoutBorderRight: number
    hasNewLayout: boolean
    isDirty: boolean
    isReferenceBaseline: boolean
    scrollLeft: number
    scrollTop: number
    scrollableWidth: number
    scrollableHeight: number
    isClipping: boolean
    isDisplayed: boolean
    visualParent: Node|nil
    addChild: ((self: TextInput, child: Node) -> nil)
    insertAt: ((self: TextInput, index: integer, child: Node) -> nil)
    removeAt: ((self: TextInput, index: integer) -> nil)
    scrollIntoView: ((self: TextInput) -> nil)
    focus: ((self: TextInput) -> nil)
    blur: ((self: TextInput) -> nil)
end

declare TextInput: {
}

declare extern type View extends Component with
    visualChildren: { Node }
    canHaveChildren: boolean
    name: string
    isFocusable: boolean
    layoutMarginPosition: LuaVector2
    layoutMarginSize: LuaVector2
    layoutBorderPosition: LuaVector2
    layoutBorderSize: LuaVector2
    layoutPaddingPosition: LuaVector2
    layoutPaddingSize: LuaVector2
    layoutContentPosition: LuaVector2
    layoutContentSize: LuaVector2
    layoutMargin: LuaVector2
    layoutPadding: LuaVector2
    layoutBorder: LuaVector2
    scissorRect: LuaRect
    layoutWidth: number
    layoutHeight: number
    layoutX: number
    layoutY: number
    layoutDirection: Direction
    hadOverflow: boolean
    layoutMarginTop: number
    layoutMarginBottom: number
    layoutMarginLeft: number
    layoutMarginRight: number
    layoutPaddingTop: number
    layoutPaddingBottom: number
    layoutPaddingLeft: number
    layoutPaddingRight: number
    layoutBorderTop: number
    layoutBorderBottom: number
    layoutBorderLeft: number
    layoutBorderRight: number
    hasNewLayout: boolean
    isDirty: boolean
    isReferenceBaseline: boolean
    scrollLeft: number
    scrollTop: number
    scrollableWidth: number
    scrollableHeight: number
    isClipping: boolean
    isDisplayed: boolean
    visualParent: Node|nil
    addChild: ((self: View, child: Node) -> nil)
    insertAt: ((self: View, index: integer, child: Node) -> nil)
    removeAt: ((self: View, index: integer) -> nil)
    scrollIntoView: ((self: View) -> nil)
    focus: ((self: View) -> nil)
    blur: ((self: View) -> nil)
end

declare View: {
}

declare extern type BaseMouseDragEvent with
    dragStart: LuaVector2
    position: LuaVector2
    button: integer
    buttons: MouseButtons
    ctrlKey: boolean
    metaKey: boolean
    shiftKey: boolean
    __eq: ((left: BaseMouseDragEvent, right: BaseMouseDragEvent) -> boolean)
end

declare BaseMouseDragEvent: {
}

declare extern type BaseMouseEvent with
    position: LuaVector2
    button: MouseButton
    buttons: MouseButtons
    ctrlKey: boolean
    altKey: boolean
    shiftKey: boolean
    __eq: ((left: BaseMouseEvent, right: BaseMouseEvent) -> boolean)
end

declare BaseMouseEvent: {
}

declare extern type BaseMouseMoveEvent with
    position: LuaVector2
    buttons: MouseButtons
    ctrlKey: boolean
    altKey: boolean
    shiftKey: boolean
    __eq: ((left: BaseMouseMoveEvent, right: BaseMouseMoveEvent) -> boolean)
end

declare BaseMouseMoveEvent: {
}

declare extern type BaseMouseWheelEvent with
    delta: LuaVector3
    position: LuaVector2
    buttons: MouseButtons
    ctrlKey: boolean
    metaKey: boolean
    shiftKey: boolean
    __eq: ((left: BaseMouseWheelEvent, right: BaseMouseWheelEvent) -> boolean)
end

declare BaseMouseWheelEvent: {
}

declare extern type KeyboardEvent with
    keyChar: Key
    keyCode: Key
    keys: Keys
    __eq: ((left: KeyboardEvent, right: KeyboardEvent) -> boolean)
end

declare KeyboardEvent: {
}

declare extern type KeyboardTypingEvent with
    keyChar: integer
    __eq: ((left: KeyboardTypingEvent, right: KeyboardTypingEvent) -> boolean)
end

declare KeyboardTypingEvent: {
}

declare extern type MouseDragEvent with
    dragStart: LuaVector2
    relativeDragStart: LuaVector2
    position: LuaVector2
    button: integer
    buttons: MouseButtons
    ctrlKey: boolean
    metaKey: boolean
    shiftKey: boolean
    relativePosition: LuaVector2
    __eq: ((left: MouseDragEvent, right: MouseDragEvent) -> boolean)
end

declare MouseDragEvent: {
}

declare extern type MouseEvent with
    position: LuaVector2
    button: MouseButton
    buttons: MouseButtons
    ctrlKey: boolean
    metaKey: boolean
    shiftKey: boolean
    relativePosition: LuaVector2
    __eq: ((left: MouseEvent, right: MouseEvent) -> boolean)
end

declare MouseEvent: {
}

declare extern type MouseMoveEvent with
    position: LuaVector2
    buttons: MouseButtons
    ctrlKey: boolean
    metaKey: boolean
    shiftKey: boolean
    relativePosition: LuaVector2
    __eq: ((left: MouseMoveEvent, right: MouseMoveEvent) -> boolean)
end

declare MouseMoveEvent: {
}

declare extern type MouseWheelEvent with
    delta: LuaVector3
    position: LuaVector2
    buttons: MouseButtons
    ctrlKey: boolean
    metaKey: boolean
    shiftKey: boolean
    relativePosition: LuaVector2
    __eq: ((left: MouseWheelEvent, right: MouseWheelEvent) -> boolean)
end

declare MouseWheelEvent: {
}

declare extern type Direction with
end

declare Direction: {
    inherit: Direction,
    ltr: Direction,
    rtl: Direction,
}

declare extern type TextNode extends Node with
    visualChildren: { Node }
    text: string
    visualParent: Node|nil
end

declare TextNode: {
}

