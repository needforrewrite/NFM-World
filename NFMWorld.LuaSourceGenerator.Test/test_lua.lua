local obj = SampleClass.new(42, "TestName")
print("Created object, type = " .. type(obj))
if obj == nil then
    print("obj is nil!")
    return 0, ""
end
print("Id before reading = " .. tostring(obj.id))
local id = obj.id
print("Id after reading = " .. tostring(id))
local name = obj.name
print("Name = " .. tostring(name))
return id, name
