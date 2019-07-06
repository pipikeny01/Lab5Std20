using MyHelloViewObjects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MyHelloCoreBO
{
    public class Service
    {
        public string GetHelloWorld()
        {
            return "Hello Worlds !!";
        }


        public IEnumerable<Employee> GetEmployees(Employee emp)
        {
            return new Employee[]
            {
                new Employee(){EmpID = 1001, EmpName = "Kenny" , BirthDay = new DateTime(1981,7,8) },
                new Employee(){EmpID = 1002, EmpName = "Hanrry" , BirthDay = new DateTime(2000,1,1) },
                new Employee(){EmpID = 1003, EmpName = "Terry" , BirthDay = new DateTime(1979,1,1) }
            }.Where(p=>p.EmpID == emp.EmpID || p.EmpName==emp.EmpName || p.BirthDay == emp.BirthDay);
        }

    }

}
