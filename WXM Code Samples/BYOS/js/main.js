// configuration Settings for BYOS
var config = {
  baseURL: "https://api.getcloudcherry.com",
  SuveyToken: "", //Pass survey token here created using Postman
  securityPassphrase: "", //Pass security Passphrase used during survey token creation

  responses: [
    {
      id: "text1",
      questionId: "5ba8c4c5b54b701a787eeb0f",
      questionText:
        "How likely are you to recommend Webex to your friends and family?",
      questionType: "Number",
      valueid: "nps-value",
    },
    {
      id: "text2",
      questionId: "5ec6b9f735212444805792bc",
      questionText: "Overall Ratings",
      questionType: "Number",
      valueid: "rating",
    },
    {
      id: "text3",
      questionId: "5bb20dca7e2ce420c03215ae",
      questionText: "Comments",
      questionType: "Text",
      valueid: "comments",
    },
  ],
};

prefills = [];

window.onload = function () {
  // //All Prefills should go here
  var prefill1 = {
	questionId: "5baa1126ff7ece0e9060de74",
    answer: "Testing Prefills",
  };

  prefills.push(prefill1);
  document.getElementById("w3mission").value = '';
};



// variable declaration
var hoverValue;
var oAuthToken;
var user;
var npsValue;
var imgLength;
var questiondisplayText = [];

var response = [];

var user;
// Passing the questiontext in the question

var value = config.responses;
for (var i = 0; i < config.responses.length; i++) {
  document.getElementById(value[i].id).innerHTML =
   value[i].questionText;
}

//while clicking showing and hiding the solid star images and outline star images

$(".blocks > div img").click(function (event) {
  imgLength = event.target.alt;
  valueid = event.target.parentElement.parentElement.id;
  config.responses.filter(function(el) { return el.valueid === valueid})[0].value = imgLength;
  for (var i = 0; i <= imgLength; i++) {
    if ($(".blocks > div:nth-child("+ i + ") > img:nth-child(2)").is(":visible")) {
      $(".blocks > div > img:nth-child(2").hide();
      $(".blocks > div > img:nth-child(1)").show();
    }

    $(".blocks > div:nth-child(" + i + ") > img:nth-child(1)").hide();
    $(".blocks > div:nth-child(" + i +") > img:nth-child(2)").show();
  }
});

// changing the background color for the NPS button while hovering
$(".scale-buttons > div").click(function (event) {
  // setting the default color for NPS Background
  $(".scale-buttons > .dec").css("background-color", "rgb(238, 83, 65)");
  $(".scale-buttons > .passive").css("background-color", "rgb(255, 188, 0)");
  $(".scale-buttons > .promoter").css("background-color", "rgb(126, 183, 127)");
  npsValue = event.target.innerText;
  valueid = event.target.parentElement.id;
  config.responses.filter(function(el) { return el.valueid === valueid})[0].value = npsValue;
  // NPS dectactor
  if (
    npsValue == 0 ||
    npsValue == 1 ||
    npsValue == 2 ||
    npsValue == 3 ||
    npsValue == 4 ||
    npsValue == 5 ||
    npsValue == 6
  ) {
    $(this).css("background-color", "rgb(196, 68, 65)");
  } else if (npsValue == 7 || npsValue == 8) {
    $(this).css("background-color", "rgb(230, 171, 17)");
  } else {
    $(this).css("background-color", "rgb(104, 153, 107)");
  }
});

// posting the submitted response to the WXM Product
function postSurvey() {
  document.getElementById('submit-survey').disabled = true;
  var responseDateTimeValue = new Date();
var answerId;

// Hashing answerId field
var sign = config.securityPassphrase;
var surveyToken = config.SuveyToken;
if (sign !== undefined) {
  var ticks = responseDateTimeValue.getTime() * 10000 + 621355968000000000;
  var hash = CryptoJS.HmacSHA256(ticks + ";" + surveyToken, sign);
  answerId = hash.toString(CryptoJS.enc.Base64);
}
  config.responses.filter(
    function(el) {  return el.valueid == "comments"}
  )[0].value = document.getElementById("w3mission").value;
  textbox = document.getElementById("w3mission").value;
  if (npsValue == undefined && imgLength == undefined && textbox == "") {
    alert("Please complete your survey");
    document.getElementById('submit-survey').disabled = false;
  } else {
    // Collecting the date format;
    // creating responses Json for the post survey
    var responses1 = [];
    for (var i = 0; i < config.responses.length; i++) {
      if(config.responses[i].value != null && config.responses[i].value != "" ){
      var res = {
        questionId: config.responses[i].questionId,
        questionText: config.responses[i].questionText,
        textInput:
          config.responses[i].questionType == "Text"
            ? config.responses[i].value
            : "",
        numberInput:
          config.responses[i].questionType == "Number"
            ? parseInt(config.responses[i].value)
            : 0,
      };
      responses1.push(res);
    }
    }
    for (var i = 0; i < prefills.length; i++) {
      var res = {
        questionId: prefills[i].questionId,
        textInput: prefills[i].answer,
      };
      responses1.push(res);
    }
    // adding the responses to post survey API
    var object = {
      Id: answerId,
      responseDateTime: responseDateTimeValue,
      responses: responses1,
    };
    var settings = {
      // post the required field to the SurveyBytoken API
      async: true,
      crossDomain: true,
      url: config.baseURL + "/api/SurveyByToken/" + config.SuveyToken,
      method: "POST",

      headers: {
        "Content-Type": "application/json",
      },
      data: JSON.stringify(object),
      error: function (xhr, error) {
        alert("We are unable to submit your response. Please try again."); // error message alter
      },
    };
    $.ajax(settings).done(function (response) {
      if (response != null) {
        removePreviousValue();
      }
      else
      {
        alert("We are unable to submit your response. Please try again."); // error message alter
        document.getElementById('submit-survey').disabled = false;
      }
    });
    // function call to hide the single survey page
  }
}
function removePreviousValue() {
  // showing the thank you after submitting the feedback
  $("#main-page").hide();
  $("#thank-you").show();
}
