window.addEventListener("engine-loaded", () => {
    console.log("running main");
    StartScript(); 
});

let plr = null;
let tru_spd = null

function StartScript(){
    
    // Import engine library
    const Engine = window.Engine;
    const Shape = window.Shape;
    const Color = window.Color;
    
    let player = new Shape("rect", Color.Black);
    plr = player;
    player.transform.position = new Vector2(200, 200);
    player.transform.scale = new Vector2(75, 75);

    let true_speed = 40;
    tru_spd = true_speed;
    let speed = true_speed * Engine.deltaTime;

    // for input duh.
    const keys = {
        "w": false,
        "s": false,
        "a": false,
        "d": false
    }
    // input listeners
    addEventListener('keydown', function(event){keys[event.key] = true});
    addEventListener('keyup', function(event){keys[event.key] = false});


    // called on engine start
    // unity reference?
    function Start(){
        Engine.AddShape(player); 
    }

    // called every engine update at TargetFPS rate
    function Update(){
        speed = true_speed * Engine.deltaTime;

        if(keys["w"]){
            player.transform.position.y -= speed;
        }
        if(keys["s"]){
            player.transform.position.y += speed;
        }
        if(keys["a"]){
            player.transform.position.x -= speed;
        }
        if(keys["d"]){
            player.transform.position.x += speed;
        }
    }

    // Add methods above into Engine for it to run
    Engine.SetTargetFPS(50);
    Engine.AddStart(Start);
    Engine.AddUpdate(Update);

    // Starts engine
    Engine.Start("canvas");
}

