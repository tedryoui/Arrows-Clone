void sdRoundedIsoscelesTriangle_float(float2 p, float2 q, float r, out float value)
{
    p.x = abs(p.x);
    float2 a = p - q * clamp(dot(p, q) / dot(q, q), 0.0, 1.0);
    float2 b = p - q * float2(clamp(p.x / q.x, 0.0, 1.0), 1.0);
    float s = -sign(q.y);
    float2 d = min(float2(dot(a, a), s * (p.x * q.y - p.y * q.x)), 
                   float2(dot(b, b), s * (p.y - q.y)));
                   
    value = -sqrt(d.x) * sign(d.y) - r;
}

void sdRoundedBox_float( in float2 p, in float2 b, in float4 r, out float value)
{
    r.xy = (p.x>0.0)?r.xy : r.zw;
    r.x  = (p.y>0.0)?r.x  : r.y;
    float2 q = abs(p)-b+r.x; 
    value = min(max(q.x,q.y),0.0) + length(max(q,0.0)) - r.x;
}