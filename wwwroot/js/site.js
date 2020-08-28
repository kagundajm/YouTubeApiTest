function uploadVideo() {
  const uploadProgress = document.getElementById("uploadProgress")
  const uploadError = document.getElementById("uploadError")
  const videoIdContainer = document.getElementById("videoIdContainer")
  const uploadMessage = document.getElementById("uploaded")
  const uploadedPercent = document.getElementById("percent")
  const progress = document.getElementById("progress")
  const videoId = document.getElementById("videoId")

  const formData = new FormData(form);
  const xhr = new XMLHttpRequest();

  var youtubeMonitor;

  function trackVideoProcessingProgress() {
    fetch('/videos/upload?handler=progress')
      .then(response => {
        if (response.status === 200) {
          response.text().then(data => {
            console.log(' response data: ', data)
            uploadMessage.innerHTML = `Processing uploaded video.`
            uploadedPercent.innerHTML = `( ${data}%)`
            progress.value = data
          })
        } else {
          console.log('Error response: ', response)
          let errorMsg = `An error ocurred while processing video (status = ${response.status})`
          showError(errorMsg);
        }
      })
  }

  // lets track the upload progress
  xhr.upload.onprogress = function (evt) {
    let loaded = parseInt(evt.loaded) / 1000000;
    let total = parseInt(evt.total) / 1000000;
    let percentValue = (loaded / total * 100).toFixed(1);

    if (uploadProgress.classList.contains('invisible')) {
      uploadProgress.classList.remove('invisible')
      uploadProgress.classList.add('visible')
    }
    uploadMessage.innerHTML = `Uploaded ${loaded.toFixed(1)}MB of ${total.toFixed(1)}MB`
    uploadedPercent.innerHTML = `( ${percentValue}%)`
    console.log(`Uploaded ${loaded.toFixed(1)}MB of ${total.toFixed(1)}MB`)
    if (parseInt(percentValue) >= 1) {
      console.log(`progress value: ${percentValue}%`)
      progress.value = percentValue

      if (parseInt(percentValue) >= 100) {
        youtubeMonitor = setInterval(trackVideoProcessingProgress, 1000)
      }
    }
  };

  xhr.onloadend = function () {
    let responseJson = xhr.response;
    if (xhr.status == 200) {
      clearInterval(youtubeMonitor)
      console.log(' response Json object: ', responseJson)
      console.log(' Video Id: ', responseJson.id)

      if (responseJson.status === 'completed') {
        videoIdContainer.classList.remove('invisible')
        videoIdContainer.classList.add('visible')
        videoId.innerHTML = responseJson.id

        uploadProgress.classList.add('visible')
        uploadProgress.classList.remove('invisible')

        // Clear form values

        setTimeout(() => {
          uploadProgress.classList.add('invisible')
          videoIdContainer.classList.add('invisible')
        }, 4000)
      } else {
      console.log(' response Json object: ', responseJson)
        showError(responseJson.errorMessage);
        console.log(' responseJson.errorMessage: ', responseJson.errorMessage)

      }
    } else {
      let error = `An error ocurred during video upload (status = ${xhr.status})`
      showError(error);

      console.log(` error returned. status: ${xhr.status} responseJson:${xhr.statusText} `)
    }
  }

  xhr.open('post', '/videos/upload');
  xhr.responseType = 'json';
  xhr.send(formData);

  function showError(errorMessage) {
    uploadError.innerHTML = errorMessage
    uploadProgress.classList.add('invisible');
    uploadError.classList.remove('invisible');
    uploadError.classList.add('visible');

    setTimeout(() => {
      uploadError.classList.add('invisible');
      uploadError.classList.remove('visible');
    }, 3000);
  }
}