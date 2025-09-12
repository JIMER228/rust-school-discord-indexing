(function(){
    var parent = document.getElementById("fire_sparks");
    var styles = document.getElementById("fire_sparks_styles");
    var total_sparks = 69;
    var random_size_and_speed = true; //parameters are in the for loop below
    
    var small_random = function(min, max) {
      return (Math.random() * (max - min) + min).toFixed(2);
    }
    var big_random = function(min, max){
      return (Math.random() * (max - min) + min).toFixed(0);
    }
    
    // the HTML creator function
    var spark_HTML = function(index, inner_speed, inner_delay, spark_travel_speed, spark_travel_delay) {
      let fixed_index;
      let fixed_inner_speed;
      // check if we random_size_and_speed is true or not
      random_size_and_speed == false ? (fixed_index = '', fixed_inner_speed = 2000) : (fixed_index = index, fixed_inner_speed = inner_speed);
      // we are returning the HTML code for each spark here every time the spark_HTML function is called by the loop below
      return '<div class="spark" style="animation-name: travel'+index+'; animation-duration: '+spark_travel_speed+'ms; animation-delay: '+spark_travel_delay+'ms;"><div class="spark_inner" style="animation-name: scaling'+fixed_index+'; animation-duration: '+fixed_inner_speed+'ms; animation-delay:'+inner_delay+'ms;"></div></div>';
    }
  
    // loop to create all sparks
    
    for(let i=0; i<(total_sparks+1); i++) {
      // if random random_size_and_speed is not activated we add the keyframe once
      if (random_size_and_speed == false && i == 0) {
        styles.innerHTML = "@keyframes scaling {0% {transform: scale3d(0.4, 0.4, 1);} 50% {transform: scale3d(1.4, 1.4, 1);} 100% {transform: scale3d(0.4, 0.4, 1);}}";
      }
      // if random_size_and_speed is activated we add a different value keyframe for each spark
      if (random_size_and_speed == true) {
        let min = small_random(0.1, 0.4); // set the min size value range
        let max = small_random(1, 1.5); // set the max size value range
        styles.innerHTML += "@keyframes scaling"+i+" {0% {transform: scale3d("+min+", "+min+", 1);} 50% {transform: scale3d("+max+", "+max+", 1);} 100% {transform: scale3d("+min+", "+min+", 1);}}";
      }
      
      /*// random travel directions; play around with the coordinates, they are expressed in CSS vh and vw units, you can also remove or add more keyframes, just make sure you add the correct number of variables as well
      let point1w = big_random(0, 100);
      let point1h = big_random(0, 60);
      let point2w = big_random(0, 100);
      let point2h = big_random(60, 100);
      let point3w = big_random(0, 130);
      let point3h = big_random(100, 130);
      styles.innerHTML += "@keyframes travel"+i+" {0% {transform: translate3d("+point1w+"vw, "+point1h+"vh, 0)} 50% {transform: translate3d("+point2w+"vw, "+point2h+"vh, 0)} 100% {transform: translate3d("+point3w+"vw, -"+point3h+"vh, 0)}}";*/
      
      
      // random travel directions; play around with the coordinates, they are expressed in CSS vh and vw units, you can also remove or add more keyframes, just make sure you add the correct number of variables as well
      let random_sign = function () { return Math.cos(Math.PI * Math.round(Math.random())); } // we need random negative or positive values for future positions of sparks
      let point1w = big_random(0, 100); // this is the X position of the initial spark
      let point1h = 103; // this is the Y position of the initial spark
      let point2w = big_random(0, 100);
      let point2h = -3;
      styles.innerHTML += "@keyframes travel"+i+" { 0% {transform: translate("+point1w+"vw, "+point1h+"vh)} 100% {transform: translate("+point2w+"vw, "+point2h+"vh);} }";    
      
      let inner_speed = big_random(1500, 2000); // miliseconds
      // call the random function to get a random inner_delay
      let inner_delay = big_random(1, 1200); // miliseconds
      // call the random function to get a random number for this spark's travel duration
      let speed = big_random(6000, 18000); // miliseconds
      // call the random function to get a random number for this spark's travel speed
      let delay = big_random(1, 15000); // miliseconds
      
  
      // duplicate the HTML structure for the fire sparks but with different parameters every time
      parent.innerHTML += spark_HTML(i, inner_speed, inner_delay, speed, delay);
    }
  })();