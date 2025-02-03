using BulletSharp;
using System.Numerics;

namespace FragBulletPhysics;

public static class Test
{
	public static void RunTest()
	{
		using var colConfiguration = new DefaultCollisionConfiguration();
		using var colDispatcher = new CollisionDispatcher(colConfiguration);
		
		//Pick broadphase algorithm
		using var broadphase = new DbvtBroadphase();
		
		//Create a collision world of your choice, for now pass null as the constraint solver
		using var colWorld = new DiscreteDynamicsWorld(colDispatcher, broadphase, null, colConfiguration);

		//Create the object shape - A 10x1x10 box in this case
		using var groundShape = new BoxShape(5f, 0.5f, 5f);

		//Use that shape to prepare object construction information
		//Initial position can be supplied into Motion state's constructor
		using var groundBodyConstructionInfo = new RigidBodyConstructionInfo(0f, new DefaultMotionState(), groundShape);

		//Create rigidbody(a physics object that can be added to the world and will collide with other objects)
		using var ground = new RigidBody(groundBodyConstructionInfo);
		
		//Register the object in the world for it to interact with other objects
		colWorld.AddCollisionObject(ground);

		//Create a shape for our ball - a sphere with radius of 2
		using var ballShape = new SphereShape(2f);

		//Ball creation info - an object with a mass of 5 and placed 10 units up on Y axis
		using var ballConstructionInfo = new RigidBodyConstructionInfo(5f, new DefaultMotionState(Matrix4x4.CreateTranslation(0f, 10f, 0f)), ballShape);

		//Create the ball object with supplied parameters
		using var ball = new RigidBody(ballConstructionInfo);

		//Add ball to the world
		colWorld.AddCollisionObject(ball);

		//Just for fun, log the ball's position
		Console.WriteLine(ball.MotionState.WorldTransform.Translation);

		//Prepare simulation parameters, try 25 steps to see the ball mid air
		var simulationIterations = 125;
		var simulationTimestep = 1f / 60f;

		//Step through the desired amount of simulation ticks
		for (var i = 0; i < simulationIterations; i++)
		{
			colWorld.StepSimulation(simulationTimestep);
		}

		//Log the ball's position after simulation happened
		//It falls down with each step and with enough steps will rest on the ground
		Console.WriteLine(ball.MotionState.WorldTransform.Translation);
	}
}
