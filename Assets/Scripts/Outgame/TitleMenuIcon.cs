using UnityEngine;
using UnityEngine.UI;

// Vector icons avoid missing glyphs in Korean UI fonts.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TitleMenuIcon : MaskableGraphic
{
    public string kind;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (kind == "▶" || kind == "▷")
        {
            if (kind == "▷") Box(vh,.12f,.2f,.88f,.8f);
            Triangle(vh,new Vector2(.36f,.28f),new Vector2(.36f,.72f),new Vector2(.73f,.5f));
        }
        else if (kind == "external")
        {
            Line(vh,.15f,.15f,.8f,.15f); Line(vh,.15f,.15f,.15f,.8f);
            Line(vh,.15f,.8f,.4f,.8f); Line(vh,.8f,.15f,.8f,.4f);
            Line(vh,.45f,.45f,.9f,.9f); Line(vh,.55f,.9f,.9f,.9f); Line(vh,.9f,.9f,.9f,.55f);
        }
        else if (kind == "▣")
        {
            Line(vh,.12f,.24f,.88f,.24f); Line(vh,.88f,.24f,.88f,.7f); Line(vh,.88f,.7f,.46f,.7f);
            Line(vh,.46f,.7f,.38f,.81f); Line(vh,.38f,.81f,.12f,.81f); Line(vh,.12f,.81f,.12f,.24f);
            Line(vh,.15f,.62f,.85f,.62f);
        }
        else if (kind == "⏻") { Arc(vh,.5f,.47f,.34f,130,410); Line(vh,.5f,.52f,.5f,.94f); }
        else if (kind == "⚙")
        {
            Arc(vh,.5f,.5f,.28f,0,360); Arc(vh,.5f,.5f,.11f,0,360);
            for(int i=0;i<8;i++) { float a=i*Mathf.PI/4; Line(vh,.5f+Mathf.Cos(a)*.26f,.5f+Mathf.Sin(a)*.26f,.5f+Mathf.Cos(a)*.4f,.5f+Mathf.Sin(a)*.4f); }
        }
        else if(kind == "◎") { Arc(vh,.5f,.5f,.36f,0,360); Line(vh,.14f,.5f,.86f,.5f); Line(vh,.5f,.14f,.5f,.86f); Arc(vh,.5f,.5f,.2f,0,360); }
        else if(kind == "▤") { Box(vh,.24f,.12f,.8f,.88f); Line(vh,.32f,.12f,.32f,.88f); Line(vh,.41f,.68f,.65f,.68f); }
        else { Line(vh,.18f,.4f,.18f,.65f); Line(vh,.18f,.65f,.78f,.85f); Line(vh,.78f,.85f,.78f,.2f); Line(vh,.78f,.2f,.18f,.4f); Line(vh,.32f,.36f,.41f,.12f); }
    }
    Vector2 Point(Vector2 p) { var r=rectTransform.rect;return new Vector2(r.xMin+p.x*r.width,r.yMin+p.y*r.height); }
    void Triangle(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c)
    { int n=vh.currentVertCount;vh.AddVert(Point(a),color,Vector2.zero);vh.AddVert(Point(b),color,Vector2.zero);vh.AddVert(Point(c),color,Vector2.zero);vh.AddTriangle(n,n+1,n+2); }
    void Line(VertexHelper vh,float ax,float ay,float bx,float by)
    {
        Vector2 a=new Vector2(ax,ay),b=new Vector2(bx,by);var d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*.035f;
        Triangle(vh,a+n,b+n,b-n);Triangle(vh,a+n,b-n,a-n);
    }
    void Box(VertexHelper v,float x,float y,float r,float t) { Line(v,x,y,r,y);Line(v,r,y,r,t);Line(v,r,t,x,t);Line(v,x,t,x,y); }
    void Arc(VertexHelper v,float x,float y,float radius,float start,float end)
    { for(int i=0;i<36;i++){float a=Mathf.Lerp(start,end,i/36f)*Mathf.Deg2Rad,b=Mathf.Lerp(start,end,(i+1)/36f)*Mathf.Deg2Rad;Line(v,x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius,x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius);} }
}
