var sendButton;
var cmdInput;
var input = [];
var inputhold = [];
var wait = false;
var path = new URL(window.location.toString().replace("#", "").replace("Crypt.html", "Test.txt"));
var num = 0;
document.onreadystatechange = Start;
document.onclick = Check;
document.onkeydown = Enter;
function Start() {
    sendButton = document.getElementById('send');
    cmdInput = document.getElementById('cmd');
    num++;
    console.log(path + " " + num);
}
function Enter(event) {
    if (event.key == "Enter") {
        Next();
    }
}
function Check(event) {
    if (event.path[0] != null && event.path[0].id == 'send')
    {
        Next();
    }
}
function Next() {
    if (cmdInput.value == "" || wait) return;
    input.push(cmdInput.value);
    inputhold.push(cmdInput.value);
    var cr = document.getElementById('response');
    if (inputhold.length == 1) {
        inputhold[0] = cr.innerText + " " + inputhold[0];
        cr.innerText = inputhold[0];
        cmdInput.value = "";
        Waiting(cr);
        return;
    }
    if (inputhold.length > 15) inputhold.shift();
    if (input.length > 45) input.shift();
    cr.innerText = inputhold.join("\n");
    cmdInput.value = "";
}
function Waiting(cr) {
    cr.innerText = cr.innerText.concat("\n Please wait...");
    wait = true;
}