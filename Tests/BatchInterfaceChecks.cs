using System.Reflection;
using SinmaiAssist.Cheat;

internal static class BatchInterfaceChecks
{
    public static void Run()
    {
        void Check(bool success,string message)
        {if(!success)throw new Exception("Batch reflection: "+message);}
        var type=typeof(SelectionMonitor);
        Type Resolve(string name)=>name==BatchInterfaces.DeployList.TypeName?type:Type.GetType(name);
        var deploy=BatchInterfaces.DeployList.Resolve(Resolve);
        var monitor=new SelectionMonitor();
        deploy.Invoke(monitor,new object[]{false,false});
        Check(monitor.Calls==1&&!monitor.Animation&&!monitor.ChangeAnimation,"deploy invokes both explicit false arguments");
        Check(deploy.GetParameters().All(p=>p.IsOptional),"fixture preserves the native optional parameter shape");
        try
        {
            BatchMethod.Require(type,"SetDeployList",typeof(bool));
            throw new Exception("Batch reflection: one-argument lookup must fail");
        }
        catch(MissingMethodException ex)
        {
            Check(ex.Message.Contains(type.FullName+".SetDeployList(System.Boolean)"),"missing method identifies the expected signature");
        }
        try
        {
            BatchMethod.Require(type,"SetDeployList",typeof(bool),typeof(string));
            throw new Exception("Batch reflection: matching parameter count with wrong types must fail");
        }
        catch(MissingMethodException ex){Check(ex.Message.Contains("System.Boolean, System.String"),"wrong-type failure identifies both expected parameters");}
        Check((string)BatchMethod.Require(type,"Folder",typeof(int)).Invoke(monitor,new object[]{0})=="index","integer overload");
        Check((string)BatchMethod.Require(type,"Folder",typeof(string)).Invoke(monitor,new object[]{"ordinary"})=="ordinary","string overload after integer lookup");
        Check((string)BatchMethod.Require(type,"Folder",typeof(int)).Invoke(monitor,new object[]{1})=="index","repeated overload lookup stays exact");
        Check((int)BatchMethod.Require(type,"Inherited",typeof(uint)).Invoke(monitor,new object[]{2u})==2,"inherited nonpublic method");
        Check(BatchMethod.Require(type,"StaticMethod").IsStatic,"static method");
        try
        {
            new BatchMethod("Missing.Native.Type","Start").Resolve(_=>null);
            throw new Exception("Batch reflection: missing type must fail");
        }
        catch(TypeLoadException ex){Check(ex.Message.Contains("Missing.Native.Type.Start()"),"missing type identifies the dependent method");}
        Console.WriteLine("PASS: production Batch signature resolver, optional-parameter regression, exact overloads, inherited methods and diagnostics.");
    }

    private class MonitorBase
    {
        protected int Inherited(uint value)=>(int)value;
    }
    private sealed class SelectionMonitor : MonitorBase
    {
        public int Calls;
        public bool Animation=true,ChangeAnimation=true;
        public void SetDeployList(bool isAnimation=true,bool isChangeAnimation=false)
        {Calls++;Animation=isAnimation;ChangeAnimation=isChangeAnimation;}
        private string Folder(int index)=>"index";
        private string Folder(string category)=>category;
        private static void StaticMethod(){}
    }
}
