DefineGamemodeConfig({
    name = "PvP",
    description = "A standard PvP free-for-all gamemode.",
    properties = {
        {
            name = "constraint",
            type = "string",
            label = "Race constraint",
            description = "The type of race to play.",
            options = {
                { label = "Racing", value = "racing" },
                { label = "Wasting", value = "wasting" },
                { label = "Both", value = "both" }
            }
        }
    }
})
