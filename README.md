#WordPressToSitefinity

This is not a standalone project. It must be placed inside of a Sitefinity 7.0 or later project and run from there.

##What it does

This tool imports blog posts from WordPress export files in to Telerik Sitefinity blogs.

**Current Functionality**

* Tags and Categories can be automatically created in Sitefinity and assigned to the posts
* Images found in post content are downloaded from their remote location, stored inside Sitefinity libraries, and included in the new blog post using [sfref]

**Upcoming Functionality**

* Import post comments
* Import post meta data (description, keywords)
* Import post thumbnail images

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

_Make sure to do a backup of your project (including the database!) before attempting the import. Just in case, ya know. :)_

1. Navigate to ~/Migration/WPM.aspx to upload your WordPress XML dump and select the options you'd like.
2. Click 'Run'
