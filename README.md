#WordPressToSitefinity

This is not a standalone project. It must be placed inside of a Sitefinity 7.0 or later project and run from there.

##Dependencies

* CSQuery

##Please note

Currently, this if intended for developers already familiar with Telerik Sitefinity as it's not quite complete.

Things that are yet to be completed are:

* Importing post meta information
* Importing comments

Suggestions and pull requests are totally welcome!

##How to hook everything up

###Copying the source files

Merge the source files into your project.

##Running the import

Make sure to do a backup of your project (including the database!) before attempting the import. Just in case, ya know. :)

Navigate to ~/Migration/WPM.aspx to upload your WordPress XML dump and select the options you'd like. After that, click 'Run' and off it goes!
